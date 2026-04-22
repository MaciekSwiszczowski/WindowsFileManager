using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Reactive.Testing;
using TUnit.Core;
using WinUiFileManager.Application.FileOperations;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Tests.Fakes;
using WinUiFileManager.Application.Tests.Fixtures;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Infrastructure.Execution;
using WinUiFileManager.Infrastructure.FileSystem;
using WinUiFileManager.Infrastructure.Services;
using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Application.Tests.Scenarios;

/// <summary>
/// Exercises the pane's Rx+DynamicData watcher pipeline with a virtual-time scheduler,
/// demonstrating that bursty watcher events (e.g. the 10 000-file copy case) coalesce
/// into a single UI commit per buffer window.
/// </summary>
public sealed class FilePaneViewModelWatcherTests
{
    private static readonly long OneSecondTicks = TimeSpan.FromSeconds(1).Ticks;
    private static readonly long BufferWindowTicks = TimeSpan.FromMilliseconds(100).Ticks;

    [Test]
    public async Task Test_Watcher_HundredCreatedEvents_CoalesceIntoOneBatch()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var (pane, scheduler, stream) = await CreateNavigatedPaneAsync(fixture);
        var watchedPath = pane.CurrentNormalizedPath!.Value;
        var commitCount = 0;
        ((System.Collections.Specialized.INotifyCollectionChanged)pane.Items).CollectionChanged +=
            (_, _) => commitCount++;
        var initialItemCount = pane.Items.Count;
        var initialCommitCount = commitCount;

        var filePaths = new List<string>();
        for (var i = 0; i < 100; i++)
        {
            filePaths.Add(fixture.CreateFile($"burst{i:D3}.txt"));
        }

        // Act: push all 100 Created events into the stream, then advance virtual
        // time past one buffer window. The Rx pipeline MUST collapse every
        // Created event that fell inside the same window into a single
        // SourceCache edit.
        foreach (var full in filePaths)
        {
            stream.Push(watchedPath, new DirectoryChange(
                DirectoryChangeKind.Created,
                NormalizedPath.FromUserInput(full)));
        }

        scheduler.AdvanceBy(OneSecondTicks);

        // Assert
        var itemDelta = pane.Items.Count - initialItemCount;
        await Assert.That(itemDelta).IsEqualTo(100);

        // 100 incremental watcher events must be coalesced to at most a handful
        // of CollectionChanged notifications (one per sorted insert position is
        // acceptable; 100 one-per-event notifications would be the regression
        // we are fixing).
        var commitDelta = commitCount - initialCommitCount;
        await Assert.That(commitDelta).IsLessThanOrEqualTo(4);
    }

    [Test]
    public async Task Test_Watcher_DeletedEvent_RemovesItem()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var targetFullPath = fixture.CreateFile("alpha.txt");
        var (pane, scheduler, stream) = await CreateNavigatedPaneAsync(fixture);
        var watchedPath = pane.CurrentNormalizedPath!.Value;

        // Act
        File.Delete(targetFullPath);
        stream.Push(watchedPath, new DirectoryChange(
            DirectoryChangeKind.Deleted,
            NormalizedPath.FromUserInput(targetFullPath)));

        scheduler.AdvanceBy(OneSecondTicks);

        // Assert
        await Assert.That(pane.Items.Any(i => !i.IsParentEntry && i.Name == "alpha.txt")).IsFalse();
    }

    [Test]
    public async Task Test_Watcher_RenamedEvent_RemovesOldAddsNew()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var oldFullPath = fixture.CreateFile("old.txt");
        var newFullPath = Path.Combine(fixture.RootPath, "new.txt");
        var (pane, scheduler, stream) = await CreateNavigatedPaneAsync(fixture);
        var watchedPath = pane.CurrentNormalizedPath!.Value;

        // Act
        new FileInfo(oldFullPath).MoveTo(newFullPath);
        stream.Push(watchedPath, new DirectoryChange(
            DirectoryChangeKind.Renamed,
            NormalizedPath.FromUserInput(newFullPath),
            NormalizedPath.FromUserInput(oldFullPath)));

        scheduler.AdvanceBy(OneSecondTicks);

        // Assert
        await Assert.That(pane.Items.Any(i => !i.IsParentEntry && i.Name == "old.txt")).IsFalse();
        await Assert.That(pane.Items.Any(i => !i.IsParentEntry && i.Name == "new.txt")).IsTrue();
    }

    [Test]
    public async Task Test_Watcher_RenamedEvent_PreservesCurrentSelectionOnRenamedItem()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var oldFullPath = fixture.CreateFile("old.txt");
        var newFullPath = Path.Combine(fixture.RootPath, "new.txt");
        var (pane, scheduler, stream) = await CreateNavigatedPaneAsync(fixture);
        var watchedPath = pane.CurrentNormalizedPath!.Value;
        var oldEntry = pane.Items.Single(i => !i.IsParentEntry && i.Name == "old.txt");
        pane.CurrentItem = oldEntry;
        oldEntry.IsSelected = true;

        new FileInfo(oldFullPath).MoveTo(newFullPath);
        stream.Push(watchedPath, new DirectoryChange(
            DirectoryChangeKind.Renamed,
            NormalizedPath.FromUserInput(newFullPath),
            NormalizedPath.FromUserInput(oldFullPath)));

        scheduler.AdvanceBy(OneSecondTicks);

        await Assert.That(pane.CurrentItem).IsNotNull();
        await Assert.That(pane.CurrentItem!.Name).IsEqualTo("new.txt");
        await Assert.That(pane.CurrentItem.IsSelected).IsTrue();
        await Assert.That(pane.SelectedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Test_Watcher_RenamedEvent_SelectsReplacementForCurrentItem()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var oldFullPath = fixture.CreateFile("old.txt");
        var newFullPath = Path.Combine(fixture.RootPath, "new.txt");
        var (pane, scheduler, stream) = await CreateNavigatedPaneAsync(fixture);
        var watchedPath = pane.CurrentNormalizedPath!.Value;
        var oldEntry = pane.Items.Single(i => !i.IsParentEntry && i.Name == "old.txt");
        pane.CurrentItem = oldEntry;

        new FileInfo(oldFullPath).MoveTo(newFullPath);
        stream.Push(watchedPath, new DirectoryChange(
            DirectoryChangeKind.Renamed,
            NormalizedPath.FromUserInput(newFullPath),
            NormalizedPath.FromUserInput(oldFullPath)));

        scheduler.AdvanceBy(OneSecondTicks);

        await Assert.That(pane.CurrentItem).IsNotNull();
        await Assert.That(pane.CurrentItem!.Name).IsEqualTo("new.txt");
        await Assert.That(pane.CurrentItem.IsSelected).IsTrue();
        await Assert.That(pane.SelectedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Test_Watcher_InvalidatedOnMissingFolder_FallsBackToExistingAncestor()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var subDir = fixture.CreateDirectory("removable");
        var (pane, scheduler, stream) = await CreateNavigatedPaneAsync(
            fixture, startInDirectory: subDir);
        var watchedPath = pane.CurrentNormalizedPath!.Value;

        // Act
        Directory.Delete(subDir, recursive: true);
        stream.Push(watchedPath, new DirectoryChange(
            DirectoryChangeKind.Invalidated,
            watchedPath));

        scheduler.AdvanceBy(OneSecondTicks);

        // Assert
        var currentPath = pane.CurrentNormalizedPath!.Value.DisplayPath;
        await Assert.That(currentPath).IsEqualTo(fixture.RootPath);
    }

    private static async Task<(FilePaneViewModel pane, TestScheduler scheduler, FakeDirectoryChangeStream stream)>
        CreateNavigatedPaneAsync(
            NtfsTempDirectoryFixture fixture,
            string? startInDirectory = null)
    {
        var scheduler = new TestScheduler();
        var schedulerProvider = new TestSchedulerProvider(scheduler);
        var pathService = new WindowsPathNormalizationService();
        var fsService = new WindowsFileSystemService(
            pathService, NullLogger<WindowsFileSystemService>.Instance);
        var volumePolicy = new NtfsVolumePolicyService(new VolumeInterop());
        var openEntry = new OpenEntryCommandHandler(
            fsService, volumePolicy, new FakeShellService(),
            NullLogger<OpenEntryCommandHandler>.Instance);
        var renameHandler = new RenameEntryCommandHandler(
            new WindowsFileOperationService(new FileOperationInterop(), NullLogger<WindowsFileOperationService>.Instance),
            NullLogger<RenameEntryCommandHandler>.Instance);
        var stream = new FakeDirectoryChangeStream();

        var pane = new FilePaneViewModel(
            openEntry,
            renameHandler,
            fsService,
            stream,
            schedulerProvider,
            volumePolicy,
            pathService,
            NullLogger<FilePaneViewModel>.Instance);

        var navTask = pane.NavigateToCommand.ExecuteAsync(startInDirectory ?? fixture.RootPath);
        scheduler.AdvanceBy(OneSecondTicks);
        await navTask;

        return (pane, scheduler, stream);
    }
}
