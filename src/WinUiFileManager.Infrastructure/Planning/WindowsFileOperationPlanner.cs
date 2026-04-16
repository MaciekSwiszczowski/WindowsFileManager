using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Operations;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Infrastructure.Planning;

public sealed class WindowsFileOperationPlanner : IFileOperationPlanner
{
    private readonly ILogger<WindowsFileOperationPlanner> _logger;

    public WindowsFileOperationPlanner(ILogger<WindowsFileOperationPlanner> logger)
    {
        _logger = logger;
    }

    public Task<OperationPlan> PlanCopyAsync(
        IReadOnlyList<FileSystemEntryModel> items,
        NormalizedPath destination,
        CollisionPolicy collisionPolicy,
        ParallelExecutionOptions parallelOptions,
        CancellationToken cancellationToken)
    {
        var planItems = BuildCopyMoveItems(items, destination, cancellationToken);

        return Task.FromResult(new OperationPlan(
            OperationType.Copy, planItems, destination, collisionPolicy, parallelOptions));
    }

    public Task<OperationPlan> PlanMoveAsync(
        IReadOnlyList<FileSystemEntryModel> items,
        NormalizedPath destination,
        CollisionPolicy collisionPolicy,
        ParallelExecutionOptions parallelOptions,
        CancellationToken cancellationToken)
    {
        var planItems = BuildCopyMoveItems(items, destination, cancellationToken);

        return Task.FromResult(new OperationPlan(
            OperationType.Move, planItems, destination, collisionPolicy, parallelOptions));
    }

    public Task<OperationPlan> PlanDeleteAsync(
        IReadOnlyList<FileSystemEntryModel> items,
        CancellationToken cancellationToken)
    {
        var planItems = new List<OperationItemPlan>();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var displayPath = item.FullPath.DisplayPath;

            if (item.Kind == ItemKind.Directory)
            {
                AddDeleteItemsForDirectory(displayPath, planItems, cancellationToken);
                planItems.Add(new OperationItemPlan(item.FullPath, null, ItemKind.Directory, 0));
            }
            else
            {
                planItems.Add(new OperationItemPlan(item.FullPath, null, ItemKind.File, item.Size));
            }
        }

        return Task.FromResult(new OperationPlan(
            OperationType.Delete, planItems, null, CollisionPolicy.Ask, new ParallelExecutionOptions()));
    }

    public Task<OperationPlan> PlanCreateFolderAsync(
        NormalizedPath parentDirectory,
        string folderName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = NormalizedPath.FromUserInput(
            Path.Combine(parentDirectory.DisplayPath, folderName));

        var planItems = new List<OperationItemPlan>
        {
            new(fullPath, null, ItemKind.Directory, 0)
        };

        return Task.FromResult(new OperationPlan(
            OperationType.CreateFolder,
            planItems,
            null,
            CollisionPolicy.Ask,
            new ParallelExecutionOptions()));
    }

    private List<OperationItemPlan> BuildCopyMoveItems(
        IReadOnlyList<FileSystemEntryModel> items,
        NormalizedPath destination,
        CancellationToken cancellationToken)
    {
        var planItems = new List<OperationItemPlan>();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceName = item.Name;
            var destBase = Path.Combine(destination.DisplayPath, sourceName);

            if (item.Kind == ItemKind.Directory)
            {
                AddCopyMoveItemsForDirectory(
                    item.FullPath.DisplayPath, destBase, planItems, cancellationToken);
            }
            else
            {
                planItems.Add(new OperationItemPlan(
                    item.FullPath,
                    NormalizedPath.FromUserInput(destBase),
                    ItemKind.File,
                    item.Size));
            }
        }

        return planItems;
    }

    private static void AddCopyMoveItemsForDirectory(
        string sourceDir,
        string destDir,
        List<OperationItemPlan> items,
        CancellationToken cancellationToken)
    {
        items.Add(new OperationItemPlan(
            NormalizedPath.FromUserInput(sourceDir),
            NormalizedPath.FromUserInput(destDir),
            ItemKind.Directory,
            0));

        var nestedSourceDirs = Directory
            .EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories)
            .OrderBy(d => Path.GetRelativePath(sourceDir, d).Length)
            .ThenBy(d => d, StringComparer.OrdinalIgnoreCase);

        foreach (var subSourceDir in nestedSourceDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeDir = Path.GetRelativePath(sourceDir, subSourceDir);
            var subDestDir = Path.Combine(destDir, relativeDir);
            items.Add(new OperationItemPlan(
                NormalizedPath.FromUserInput(subSourceDir),
                NormalizedPath.FromUserInput(subDestDir),
                ItemKind.Directory,
                0));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourceDir, file);
            var fileDestPath = Path.Combine(destDir, relativePath);
            var fi = new FileInfo(file);

            items.Add(new OperationItemPlan(
                NormalizedPath.FromUserInput(file),
                NormalizedPath.FromUserInput(fileDestPath),
                ItemKind.File,
                fi.Length));
        }
    }

    private static void AddDeleteItemsForDirectory(
        string directoryPath,
        List<OperationItemPlan> items,
        CancellationToken cancellationToken)
    {
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fi = new FileInfo(file);
            items.Add(new OperationItemPlan(
                NormalizedPath.FromUserInput(file), null, ItemKind.File, fi.Length));
        }

        var subdirs = Directory.EnumerateDirectories(directoryPath, "*", SearchOption.AllDirectories)
            .OrderByDescending(d => d.Length)
            .ToList();

        foreach (var dir in subdirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Add(new OperationItemPlan(
                NormalizedPath.FromUserInput(dir), null, ItemKind.Directory, 0));
        }
    }
}
