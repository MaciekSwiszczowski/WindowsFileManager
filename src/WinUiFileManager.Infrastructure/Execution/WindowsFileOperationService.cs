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

public sealed class WindowsFileOperationService : IFileOperationService
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
        var stopwatch = Stopwatch.StartNew();

        if (plan.Type is OperationType.Copy or OperationType.Move
            && plan.DestinationDirectory is NormalizedPath requiredDestinationRoot)
        {
            if (!Directory.Exists(requiredDestinationRoot.DisplayPath))
            {
                _logger.LogWarning(
                    "Destination directory does not exist: {Path}",
                    requiredDestinationRoot.DisplayPath);
                stopwatch.Stop();
                return new OperationSummary(
                    plan.Type,
                    OperationStatus.Failed,
                    plan.Items.Count,
                    SucceededCount: 0,
                    FailedCount: plan.Items.Count,
                    WarningCount: 0,
                    SkippedCount: 0,
                    WasCancelled: false,
                    stopwatch.Elapsed,
                    ItemResults: [],
                    Message: $"Destination folder not found: {requiredDestinationRoot.DisplayPath}");
            }
        }

        var results = new ConcurrentBag<OperationItemResult>();
        var completedCount = 0;
        var wasCancelled = false;

        try
        {
            if (plan.ParallelOptions.Enabled && plan.ParallelOptions.MaxDegreeOfParallelism > 1)
            {
                completedCount = await ExecuteParallelAsync(plan, results, progress, cancellationToken);
            }
            else
            {
                completedCount = ExecuteSequential(plan, results, progress, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
            _logger.LogWarning(
                "Operation {OperationType} cancelled after {Completed}/{Total} items",
                plan.Type, completedCount, plan.Items.Count);
        }

        if (plan.Type == OperationType.Move && !wasCancelled)
        {
            CleanupMoveSourceDirectories(plan, results);
        }

        stopwatch.Stop();

        var itemResults = results.ToList();
        var succeeded = itemResults.Count(r => r.Succeeded);
        var failed = itemResults.Count(r => !r.Succeeded);
        var warnings = itemResults.Count(r => r.Warning is not null);

        return new OperationSummary(
            Type: plan.Type,
            Status: DetermineStatus(succeeded, failed, warnings, wasCancelled),
            TotalItems: plan.Items.Count,
            SucceededCount: succeeded,
            FailedCount: failed,
            WarningCount: warnings,
            SkippedCount: plan.Items.Count - succeeded - failed,
            WasCancelled: wasCancelled,
            Duration: stopwatch.Elapsed,
            ItemResults: itemResults,
            Message: null);
    }

    private async Task<int> ExecuteParallelAsync(
        OperationPlan plan,
        ConcurrentBag<OperationItemResult> results,
        IProgress<OperationProgressEvent>? progress,
        CancellationToken cancellationToken)
    {
        var completed = 0;

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
                    continue;

                cancellationToken.ThrowIfCancellationRequested();
                var dirResult = ExecuteItem(plan.Type, item);
                results.Add(dirResult);
                completed++;
                ReportProgress(progress, plan, completed);
            }

            await Parallel.ForEachAsync(
                plan.Items.Where(i => i.Kind == ItemKind.File),
                parallelOptions,
                (item, _) =>
                {
                    var fileResult = ExecuteItem(plan.Type, item);
                    results.Add(fileResult);
                    var current = Interlocked.Increment(ref completed);
                    ReportProgress(progress, plan, current);
                    return ValueTask.CompletedTask;
                });

            return completed;
        }

        await Parallel.ForEachAsync(plan.Items, parallelOptions, (item, _) =>
        {
            var result = ExecuteItem(plan.Type, item);
            results.Add(result);
            var current = Interlocked.Increment(ref completed);
            ReportProgress(progress, plan, current);
            return ValueTask.CompletedTask;
        });

        return completed;
    }

    private int ExecuteSequential(
        OperationPlan plan,
        ConcurrentBag<OperationItemResult> results,
        IProgress<OperationProgressEvent>? progress,
        CancellationToken cancellationToken)
    {
        var completedCount = 0;

        foreach (var item in plan.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = ExecuteItem(plan.Type, item);
            results.Add(result);
            completedCount++;
            ReportProgress(progress, plan, completedCount);
        }

        return completedCount;
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
                Succeeded: true,
                Error: null,
                Warning: null);
        }

        _logger.LogError(
            "Operation {Type} failed for {Path}: error {ErrorCode} - {Message}",
            type, item.SourcePath.DisplayPath, interopResult.NativeErrorCode, interopResult.ErrorMessage);

        var error = new OperationError(
            item.SourcePath,
            MapErrorCode(interopResult.NativeErrorCode),
            interopResult.ErrorMessage ?? "Unknown error",
            interopResult.NativeErrorCode);

        return new OperationItemResult(
            item.SourcePath,
            item.DestinationPath,
            Succeeded: false,
            Error: error,
            Warning: null);
    }

    private void CleanupMoveSourceDirectories(
        OperationPlan plan,
        ConcurrentBag<OperationItemResult> results)
    {
        var failedPaths = new HashSet<string>(
            results.Where(r => !r.Succeeded).Select(r => r.SourcePath.DisplayPath),
            StringComparer.OrdinalIgnoreCase);

        var directoriesToRemove = plan.Items
            .Where(i => i.Kind == ItemKind.Directory && !failedPaths.Contains(i.SourcePath.DisplayPath))
            .Select(i => i.SourcePath.DisplayPath)
            .OrderByDescending(p => p.Length)
            .ToList();

        foreach (var dir in directoriesToRemove)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove source directory after move: {Path}", dir);
            }
        }
    }

    private static void ReportProgress(
        IProgress<OperationProgressEvent>? progress,
        OperationPlan plan,
        int completedCount)
    {
        progress?.Report(new OperationProgressEvent(
            plan.Type,
            plan.Items.Count,
            completedCount,
            TotalBytes: 0,
            CompletedBytes: 0,
            CurrentItemPath: null,
            StatusMessage: null));
    }

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
