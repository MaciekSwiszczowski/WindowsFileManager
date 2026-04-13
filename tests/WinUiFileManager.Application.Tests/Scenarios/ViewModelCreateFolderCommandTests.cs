namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelCreateFolderCommandTests
{
    [Test]
    public async Task Test_CreateFolderCommand_CreatesFolderAndRefreshes()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        var destDir = fixture.CreateDirectory("dest");
        var expectedFolder = Path.Combine(sourceDir, "MyNewFolder");

        var builder = new ViewModelTestBuilder();
        builder.DialogService.CreateFolderResult = "MyNewFolder";
        var vm = builder.Build();
        vm.LeftPane.PaneId = PaneId.Left;
        vm.RightPane.PaneId = PaneId.Right;

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);
        await vm.RightPane.NavigateToCommand.ExecuteAsync(destDir);

        // Act
        await vm.CreateFolderCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(Directory.Exists(expectedFolder)).IsTrue();
    }
}
