namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelCopyPathCommandTests
{
    [Test]
    public async Task Test_CopyPathCommand_CopiesToClipboard()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        var destDir = fixture.CreateDirectory("dest");
        var filePath = fixture.CreateFile("source/myfile.txt", sizeInBytes: 16);

        var builder = new ViewModelTestBuilder();
        var vm = builder.Build();
        vm.LeftPane.PaneId = PaneId.Left;
        vm.RightPane.PaneId = PaneId.Right;

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);
        await vm.RightPane.NavigateToCommand.ExecuteAsync(destDir);

        var targetEntry = vm.LeftPane.Items.First(i => i.Name == "myfile.txt");
        vm.LeftPane.CurrentItem = targetEntry;

        // Act
        await vm.CopyFullPathCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(builder.ClipboardService.LastCopiedText).IsNotNull();
        await Assert.That(builder.ClipboardService.LastCopiedText!).Contains("myfile.txt");
    }
}
