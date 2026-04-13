namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelRenameCommandTests
{
    [Test]
    public async Task Test_RenameCommand_RenamesFileAndRefreshes()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        var destDir = fixture.CreateDirectory("dest");
        var oldPath = fixture.CreateFile("source/old.txt", sizeInBytes: 32);
        var expectedNewPath = Path.Combine(sourceDir, "new.txt");

        var builder = new ViewModelTestBuilder();
        builder.DialogService.RenameResult = "new.txt";
        var vm = builder.Build();
        vm.LeftPane.PaneId = PaneId.Left;
        vm.RightPane.PaneId = PaneId.Right;

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);
        await vm.RightPane.NavigateToCommand.ExecuteAsync(destDir);

        var targetEntry = vm.LeftPane.Items.First(i => i.Name == "old.txt");
        vm.LeftPane.CurrentItem = targetEntry;

        // Act
        await vm.RenameCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(File.Exists(oldPath)).IsFalse();
        await Assert.That(File.Exists(expectedNewPath)).IsTrue();
    }
}
