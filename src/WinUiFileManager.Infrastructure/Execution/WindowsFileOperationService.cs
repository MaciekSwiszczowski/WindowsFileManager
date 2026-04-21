using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Errors;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.Operations;
using WinUiFileManager.Domain.Results;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Infrastructure.Execution;

internal sealed class WindowsFileOperationService : IFileOperationService
{
    private readonly IFileOperationInterop _fileOperationInterop;
    private readonly ILogger<WindowsFileOperationService> _logger;

    public WindowsFileOperationService(
        IFileOperationInterop fileOperationInterop,
        ILogger<WindowsFileOperationService> logger)
    {
        _fileOperationInterop = fileOperationInterop;
        _logger = logger;
    }

    public async Task<OperationSummary> ExecuteAsync(
        OperationPlan plan,
        IProgress<OperationProgressEvent>? progress,
        CancellationToken cancellationToken)
    {
        if (plan.Type is OperationType.Copy or OperationType.Move
            && plan.DestinationDirectory is NormalizedPath requiredDestinationRoot)
        {
            if (!Directory.Exists(requiredDestinationRoot.DisplayPath))
            {
                WindowsFileOperationServiceLog.DestinationDirectoryMissing(_logger, requiredDestinationRoot.DisplayPath);
                return new OperationSummary(
                    plan.Type,
                    OperationStatus.Failed,
                    plan.Items.Count,
                    0,
                    plan.Items.Count,
                    0,
                    0,
                    false,
                    TimeSpan.Zero,
                    [],
                    $"Destination folder not found: {requiredDestinationRoot.DisplayPath}");
            }
        }

        return await Task.Run(
            () => ExecuteCoreAsync(plan, progress, cancellationToken),
            CancellationToken.None);
    }

    private async Task<OperationSummary> ExecuteCoreAsync(
        OperationPlan plan,
        IProgress<OperationProgressEvent>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new ConcurrentBag<OperationItemResult>();
        var completedCount = 0;
        var wasCancelled = false;

        try
        {
            ReportProgress(
                progress,
                plan,
                completedCount: 0,
                completedBytes: 0,
                currentItemPath: plan.Items.FirstOrDefault()?.SourcePath,
                statusMessage: "Preparing operation",
                totalBytes: GetTotalBytes(plan));

            if (plan.ParallelOptions.Enabled && plan.ParallelOptions.MaxDegreeOfParallelism > 1)
            {
                var parallelResult = await ExecuteParallelAsync(
                    plan,
                    results,
                    progress,
                    cancellationToken);
                completedCount = parallelResult.CompletedCount;
            }
            else
            {
                var sequentialResult = ExecuteSequential(
                    plan,
                    results,
                    progress,
                    cancellationToken);
                completedCount = sequentialResult.CompletedCount;
            }
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
            WindowsFileOperationServiceLog.OperationCancelled(_logger, plan.Type, completedCount, plan.Items.Count);
        }

        if (plan.Type == OperationType.Move && !wasCancelled)
        {
            CleanupMoveSourceDirectories(plan, results);
        }

        stopwatch.Stop();

        var itemResults = results.ToList();
        var succeeded = itemResults.Count(static r => r.Succeeded);
        var failed = itemResults.Count(static r => !r.Succeeded);
        var warnings = itemResults.Count(static r => r.Warning is not null);

        return new OperationSummary(
            plan.Type,
            DetermineStatus(succeeded, failed, warnings, wasCancelled),
            plan.Items.Count,
            succeeded,
            failed,
            warnings,
            plan.Items.Count - succeeded - failed,
            wasCancelled,
            stopwatch.Elapsed,
            itemResults,
            null);
    }

    private async Task<(int CompletedCount, long CompletedBytes)> ExecuteParallelAsync(
        OperationPlan plan,
        ConcurrentBag<OperationItemResult> results,
        IProgress<OperationProgressEvent>? progress,
        CancellationToken cancellationToken)
    {
        var completed = 0;
        var completedBytes = 0L;
        var totalBytes = GetTotalBytes(plan);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = plan.ParallelOptions.MaxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        if (plan.Type is OperationType.Copy or OperationType.Move)
        {
            foreach (var item in plan.Items)
            {
                if (item.Kind != ItemKind.Directory)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                var dirResult = ExecuteItem(plan.Type, item);
                results.Add(dirResult);
                completed++;
                completedBytes += Math.Max(item.EstimatedSize, 0);
                ReportProgress(
                    progress,
                    plan,
                    completed,
                    completedBytes,
                    item.SourcePath,
                    dirResult.Succeeded ? "Item completed" : "Item failed",
                    totalBytes);
                cancellationToken.ThrowIfCancellationRequested();
            }

            await Parallel.ForEachAsync(
                plan.Items.Where(static i => i.Kind == ItemKind.File),
                parallelOptions,
                (item, _) =>
                {
                    var fileResult = ExecuteItem(plan.Type, item);
                    results.Add(fileResult);
                    var current = Interlocked.Increment(ref completed);
                    var currentBytes = Interlocked.Add(ref completedBytes, Math.Max(item.EstimatedSize, 0));
                    ReportProgress(
                        progress,
                        plan,
                        current,
                        currentBytes,
                        item.SourcePath,
                        fileResult.Succeeded ? "Item completed" : "Item failed",
                        totalBytes);
                    return ValueTask.CompletedTask;
                });

            cancellationToken.ThrowIfCancellationRequested();
            return (completed, completedBytes);
        }

        await Parallel.ForEachAsync(plan.Items, parallelOptions, (item, _) =>
        {
            var result = ExecuteItem(plan.Type, item);
            results.Add(result);
            var current = Interlocked.Increment(ref completed);
            var currentBytes = Interlocked.Add(ref completedBytes, Math.Max(item.EstimatedSize, 0));
            ReportProgress(
                progress,
                plan,
                current,
                currentBytes,
                item.SourcePath,
                result.Succeeded ? "Item completed" : "Item failed",
                totalBytes);
            return ValueTask.CompletedTask;
        });

        cancellationToken.ThrowIfCancellationRequested();
        return (completed, completedBytes);
    }

    private (int CompletedCount, long CompletedBytes) ExecuteSequential(
        OperationPlan plan,
        ConcurrentBag<OperationItemResult> results,
        IProgress<OperationProgressEvent>? progress,
        CancellationToken cancellationToken)
    {
        var completedCount = 0;
        var completedBytes = 0L;
        var totalBytes = GetTotalBytes(plan);

        foreach (var item in plan.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = ExecuteItem(plan.Type, item);
            results.Add(result);
            completedCount++;
            completedBytes += Math.Max(item.EstimatedSize, 0);
            ReportProgress(
                progress,
                plan,
                completedCount,
                completedBytes,
                item.SourcePath,
                result.Succeeded ? "Item completed" : "Item failed",
                totalBytes);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return (completedCount, completedBytes);
    }

    private OperationItemResult ExecuteItem(OperationType type, OperationItemPlan item)
    {
        var interopResult = type switch
        {
            OperationType.Copy when item.Kind == ItemKind.Directory =>
                _fileOperationInterop.CreateDirectory(item.DestinationPath!.Value.DisplayPath),
            OperationType.Copy =>
                _fileOperationInterop.CopyFile(
                    item.SourcePath.DisplayPath,
                    item.DestinationPath!.Value.DisplayPath,
                    overwrite: false),
            OperationType.Move when item.Kind == ItemKind.Directory =>
                _fileOperationInterop.CreateDirectory(item.DestinationPath!.Value.DisplayPath),
            OperationType.Rename when item.Kind == ItemKind.Directory =>
                _fileOperationInterop.MoveDirectory(
                    item.SourcePath.DisplayPath,
                    item.DestinationPath!.Value.DisplayPath),
            OperationType.Rename =>
                _fileOperationInterop.MoveFile(
                    item.SourcePath.DisplayPath,
                    item.DestinationPath!.Value.DisplayPath,
                    overwrite: false),
            OperationType.Move =>
                _fileOperationInterop.MoveFile(
                    item.SourcePath.DisplayPath,
                    item.DestinationPath!.Value.DisplayPath,
                    overwrite: false),
            OperationType.Delete when item.Kind == ItemKind.Directory =>
                _fileOperationInterop.RemoveDirectory(item.SourcePath.DisplayPath),
            OperationType.Delete =>
                _fileOperationInterop.DeleteFile(item.SourcePath.DisplayPath),
            OperationType.CreateFolder =>
                _fileOperationInterop.CreateDirectory(item.SourcePath.DisplayPath),
            _ => InteropResult.Fail(-1, $"Unsupported operation type: {type}")
        };

        if (interopResult.Success)
        {
            return new OperationItemResult(
                item.SourcePath,
                item.DestinationPath,
                true,
                null,
                null);
        }

        WindowsFileOperationServiceLog.OperationFailed(
            _logger,
            type,
            item.SourcePath.DisplayPath,
            interopResult.NativeErrorCode,
            interopResult.ErrorMessage ?? "Unknown error");

        var error = new OperationError(
            item.SourcePath,
            MapErrorCode(interopResult.NativeErrorCode),
            interopResult.ErrorMessage ?? "Unknown error",
            interopResult.NativeErrorCode);

        return new OperationItemResult(
            item.SourcePath,
            item.DestinationPath,
            false,
            error,
            null);
    }

    private void CleanupMoveSourceDirectories(
        OperationPlan plan,
        ConcurrentBag<OperationItemResult> results)
    {
        var failedPaths = new HashSet<string>(
            results.Where(static r => !r.Succeeded).Select(static r => r.SourcePath.DisplayPath),
            StringComparer.OrdinalIgnoreCase);

        var directoriesToRemove = plan.Items
            .Where(i => i.Kind == ItemKind.Directory && !failedPaths.Contains(i.SourcePath.DisplayPath))
            .Select(static i => i.SourcePath.DisplayPath)
            .OrderByDescending(static p => p.Length)
            .ToList();

        foreach (var dir in directoriesToRemove)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: false);
                }
            }
            catch (Exception ex)
            {
                WindowsFileOperationServiceLog.MoveSourceCleanupFailed(_logger, ex, dir);
            }
        }
    }

    private static void ReportProgress(
        IProgress<OperationProgressEvent>? progress,
        OperationPlan plan,
        int completedCount,
        long completedBytes,
        NormalizedPath? currentItemPath,
        string? statusMessage,
        long totalBytes)
    {
        progress?.Report(new OperationProgressEvent(
            plan.Type,
            plan.Items.Count,
            completedCount,
            totalBytes,
            completedBytes,
            currentItemPath,
            statusMessage));
    }

    private static long GetTotalBytes(OperationPlan plan) =>
        plan.Items.Sum(static item => Math.Max(item.EstimatedSize, 0));

    private static OperationStatus DetermineStatus(
        int succeeded, int failed, int warnings, bool cancelled)
    {
        if (cancelled) return OperationStatus.Cancelled;
        if (failed > 0 && succeeded == 0) return OperationStatus.Failed;
        if (failed > 0) return OperationStatus.CompletedWithErrors;
        if (warnings > 0) return OperationStatus.CompletedWithWarnings;
        return OperationStatus.Succeeded;
    }

    private static FileOperationErrorCode MapErrorCode(int nativeErrorCode) =>
        nativeErrorCode switch
        {
            5 => FileOperationErrorCode.AccessDenied,
            2 => FileOperationErrorCode.FileNotFound,
            3 => FileOperationErrorCode.DirectoryNotFound,
            206 => FileOperationErrorCode.PathTooLong,
            32 => FileOperationErrorCode.SharingViolation,
            33 => FileOperationErrorCode.FileLocked,
            80 => FileOperationErrorCode.DestinationExists,
            183 => FileOperationErrorCode.DestinationExists,
            _ => FileOperationErrorCode.Unknown,
        };
}
