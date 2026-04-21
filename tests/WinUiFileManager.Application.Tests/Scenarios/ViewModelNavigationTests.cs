using TUnit.Core;
using WinUiFileManager.Application.Tests.Fakes;
using WinUiFileManager.Application.Tests.Fixtures;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelNavigationTests
{
    [Test]
    public async Task Test_NavigateInto_Folder_ChangesCurrentPath()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var subFolder = fixture.CreateDirectory("SubFolder");
        var builder = new ViewModelTestBuilder();
        var shell = builder.Build();
        var pane = shell.LeftPane;

        await pane.NavigateToCommand.ExecuteAsync(fixture.RootPath);
        var subFolderEntry = pane.Items.First(i => i.Name == "SubFolder");
        pane.CurrentItem = subFolderEntry;

        // Act
        await pane.NavigateIntoCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(pane.CurrentPath).IsEqualTo(subFolder);
        await Assert.That(pane.Items.Any(i => i.IsParentEntry)).IsTrue();
    }

    [Test]
    public async Task Test_NavigateInto_ParentEntry_MovesUp_AndLandsOnPreviousFolder()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var subFolder = fixture.CreateDirectory("SubFolder");
        var builder = new ViewModelTestBuilder();
        var shell = builder.Build();
        var pane = shell.LeftPane;

        await pane.NavigateToCommand.ExecuteAsync(subFolder);
        var parentEntry = pane.Items.First(i => i.IsParentEntry);
        pane.CurrentItem = parentEntry;

        // Act
        await pane.NavigateIntoCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(pane.CurrentPath).IsEqualTo(fixture.RootPath);
        await Assert.That(pane.CurrentItem?.Name).IsEqualTo("SubFolder");
    }

    [Test]
    public async Task Test_SelectAll_RemainsSelected_WhenInvokedRepeatedly()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        fixture.CreateFile("one.txt");
        fixture.CreateFile("two.txt");

        var builder = new ViewModelTestBuilder();
        var shell = builder.Build();
        var pane = shell.LeftPane;

        await pane.NavigateToCommand.ExecuteAsync(fixture.RootPath);

        pane.SelectAllCommand.Execute(null);
        var firstSelectionCount = pane.SelectedCount;

        pane.SelectAllCommand.Execute(null);

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
        var shell = builder.Build();
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
        var shell = builder.Build();
        var pane = shell.LeftPane;

        await pane.NavigateToCommand.ExecuteAsync(levelThree);
        Directory.Delete(levelOne, recursive: true);

        // Act
        await pane.RefreshCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(pane.CurrentPath).IsEqualTo(fixture.RootPath);
    }
}

