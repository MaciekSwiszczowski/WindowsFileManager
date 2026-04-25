using Microsoft.Reactive.Testing;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelNavigationTests
{
    private static readonly long OneSecondTicks = TimeSpan.FromSeconds(1).Ticks;

    [Test]
    public async Task Test_NavigateInto_Folder_ChangesCurrentPath()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var subFolder = fixture.CreateDirectory("SubFolder");
        var builder = new ViewModelTestBuilder();
        using var shell = builder.Build();
        var pane = shell.LeftPane;

        await pane.NavigateToCommand.ExecuteAsync(fixture.RootPath);
        var subFolderEntry = pane.Items.First(static i => i.Name == "SubFolder");
        pane.CurrentItem = subFolderEntry;

        // Act
        await pane.NavigateIntoCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(pane.CurrentPath).IsEqualTo(subFolder);
        await Assert.That(pane.ParentEntry).IsNotNull();
    }

    [Test]
    public async Task Test_NavigateInto_Folder_SelectsParentEntryImmediatelyAndAfterLoad()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var subFolder = fixture.CreateDirectory("SubFolder");
        fixture.CreateFile(Path.Combine("SubFolder", "child.txt"));

        var scheduler = new TestScheduler();
        var (pane, _) = await CreatePaneAsync(fixture.RootPath, scheduler);
        var subFolderEntry = pane.Items.First(static i => i.Name == "SubFolder");
        pane.CurrentItem = subFolderEntry;

        var navigateTask = pane.NavigateIntoCommand.ExecuteAsync(null);

        await Assert.That(pane.CurrentPath).IsEqualTo(subFolder);
        await Assert.That(pane.IsLoading).IsTrue();
        await Assert.That(pane.CurrentItem?.Model).IsNull();
        await Assert.That(pane.CurrentItem?.Name).IsEqualTo("..");

        scheduler.AdvanceBy(OneSecondTicks);
        await navigateTask;

        await Assert.That(pane.IsLoading).IsFalse();
        await Assert.That(pane.CurrentItem?.Model).IsNull();
        await Assert.That(pane.CurrentItem?.Name).IsEqualTo("..");
    }

    [Test]
    public async Task Test_NavigateInto_ParentEntry_MovesUp_AndLandsOnPreviousFolder()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var subFolder = fixture.CreateDirectory("SubFolder");
        var builder = new ViewModelTestBuilder();
        using var shell = builder.Build();
        var pane = shell.LeftPane;

        await pane.NavigateToCommand.ExecuteAsync(subFolder);
        var parentEntry = pane.ParentEntry;
        pane.CurrentItem = parentEntry;

        // Act
        await pane.NavigateIntoCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(pane.CurrentPath).IsEqualTo(fixture.RootPath);
        await Assert.That(pane.CurrentItem?.Name).IsEqualTo("SubFolder");
    }

    [Test]
    public async Task Test_NavigateInto_MissingFolder_FallsBackToParentAndKeepsParentSelected()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var subFolder = fixture.CreateDirectory("SubFolder");
        var builder = new ViewModelTestBuilder();
        using var shell = builder.Build();
        var pane = shell.LeftPane;

        await pane.NavigateToCommand.ExecuteAsync(fixture.RootPath);
        var subFolderEntry = pane.Items.First(static i => i.Name == "SubFolder");
        Directory.Delete(subFolder, recursive: true);
        pane.CurrentItem = subFolderEntry;

        await pane.NavigateIntoCommand.ExecuteAsync(null);

        await Assert.That(pane.CurrentPath).IsEqualTo(fixture.RootPath);
        await Assert.That(pane.CurrentItem?.Model).IsNull();
        await Assert.That(pane.CurrentItem?.Name).IsEqualTo("..");
    }

    [Test]
    public async Task Test_SelectAll_RemainsSelected_WhenInvokedRepeatedly()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        fixture.CreateFile("one.txt");
        fixture.CreateFile("two.txt");

        var builder = new ViewModelTestBuilder();
        using var shell = builder.Build();
        var pane = shell.LeftPane;

        await pane.NavigateToCommand.ExecuteAsync(fixture.RootPath);

        pane.UpdateSelectionFromControl(pane.Items);
        var firstSelectionCount = pane.SelectedCount;

        pane.UpdateSelectionFromControl(pane.Items);

        await Assert.That(firstSelectionCount).IsEqualTo(2);
        await Assert.That(pane.SelectedCount).IsEqualTo(2);
    }

    [Test]
    public async Task Test_Refresh_WhenCurrentDirectoryWasDeleted_FallsBackToNearestExistingParent()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var levelOne = fixture.CreateDirectory("LevelOne");
        var levelTwo = Directory.CreateDirectory(Path.Combine(levelOne, "LevelTwo")).FullName;

        var builder = new ViewModelTestBuilder();
        using var shell = builder.Build();
        var pane = shell.LeftPane;

        await pane.NavigateToCommand.ExecuteAsync(levelTwo);
        Directory.Delete(levelTwo, recursive: true);

        // Act
        await pane.RefreshCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(pane.CurrentPath).IsEqualTo(levelOne);
    }

    [Test]
    public async Task Test_Refresh_WhenMultipleLevelsWereDeleted_FallsBackToHighestExistingParent()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var levelOne = fixture.CreateDirectory("LevelOne");
        var levelTwo = Directory.CreateDirectory(Path.Combine(levelOne, "LevelTwo")).FullName;
        var levelThree = Directory.CreateDirectory(Path.Combine(levelTwo, "LevelThree")).FullName;

        var builder = new ViewModelTestBuilder();
        using var shell = builder.Build();
        var pane = shell.LeftPane;

        await pane.NavigateToCommand.ExecuteAsync(levelThree);
        Directory.Delete(levelOne, recursive: true);

        // Act
        await pane.RefreshCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(pane.CurrentPath).IsEqualTo(fixture.RootPath);
    }

    private static async Task<(FilePaneViewModel pane, TestScheduler scheduler)> CreatePaneAsync(
        string initialPath,
        TestScheduler scheduler)
    {
        var schedulerProvider = new TestSchedulerProvider(scheduler);
        var pathService = new WindowsPathNormalizationService();
        var fsService = new WindowsFileSystemService(
            pathService,
            NullLogger<WindowsFileSystemService>.Instance);
        var volumePolicy = new NtfsVolumePolicyService(new VolumeInterop());
        var openEntry = new OpenEntryCommandHandler(
            fsService,
            volumePolicy,
            new FakeShellService(),
            NullLogger<OpenEntryCommandHandler>.Instance);
        var renameHandler = new RenameEntryCommandHandler(
            new WindowsFileOperationService(
                new FileOperationInterop(),
                NullLogger<WindowsFileOperationService>.Instance),
            NullLogger<RenameEntryCommandHandler>.Instance);
#pragma warning disable IDISP001 // Ownership of the stream instance is intentionally transferred to the pane helper graph for this test.
        var changeStream = new FakeDirectoryChangeStream();
#pragma warning restore IDISP001

        var pane = new FilePaneViewModel(
            openEntry,
            renameHandler,
            fsService,
            changeStream,
            schedulerProvider,
            volumePolicy,
            pathService,
            NullLogger<FilePaneViewModel>.Instance);

        var navigateTask = pane.NavigateToCommand.ExecuteAsync(initialPath);
        scheduler.AdvanceBy(OneSecondTicks);
        await navigateTask;

        return (pane, scheduler);
    }
}
