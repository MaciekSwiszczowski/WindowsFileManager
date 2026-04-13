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
}
