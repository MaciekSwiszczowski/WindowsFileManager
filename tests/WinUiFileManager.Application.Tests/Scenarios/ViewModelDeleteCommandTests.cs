namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelDeleteCommandTests
{
    [Test]
    public async Task Test_DeleteCommand_SingleFile_DeletesFileAndRefreshes()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        var destDir = fixture.CreateDirectory("dest");
        var filePath = fixture.CreateFile("source/target.txt", sizeInBytes: 64);

        var builder = new ViewModelTestBuilder();
        using var vm = builder.Build();
        vm.LeftPane.PaneId = PaneId.Left;
        vm.RightPane.PaneId = PaneId.Right;

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);
        await vm.RightPane.NavigateToCommand.ExecuteAsync(destDir);

        var targetEntry = vm.LeftPane.Items.First(i => i.Name == "target.txt");
        vm.LeftPane.CurrentItem = targetEntry;

        // Act
        await vm.DeleteCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(File.Exists(filePath)).IsFalse();
        await Assert.That(builder.DialogService.DeleteConfirmationCallCount).IsEqualTo(1);
        await Assert.That(builder.DialogService.ShowOperationResultCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Test_DeleteCommand_WhenDialogDeclined_DoesNotDelete()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        var destDir = fixture.CreateDirectory("dest");
        var filePath = fixture.CreateFile("source/target.txt", sizeInBytes: 64);

        var builder = new ViewModelTestBuilder();
        builder.DialogService.DeleteConfirmationResult = false;
        using var vm = builder.Build();
        vm.LeftPane.PaneId = PaneId.Left;
        vm.RightPane.PaneId = PaneId.Right;

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);
        await vm.RightPane.NavigateToCommand.ExecuteAsync(destDir);

        var targetEntry = vm.LeftPane.Items.First(i => i.Name == "target.txt");
        vm.LeftPane.CurrentItem = targetEntry;

        // Act
        await vm.DeleteCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(File.Exists(filePath)).IsTrue();
    }
}
