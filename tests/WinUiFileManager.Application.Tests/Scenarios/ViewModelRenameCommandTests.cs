namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelRenameCommandTests
{
    [Test]
    public async Task Test_RenameCommand_PrimesInlineRenameBuffer()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        fixture.CreateFile("source/old.txt", sizeInBytes: 32);

        var builder = new ViewModelTestBuilder();
        var vm = builder.Build();
        vm.LeftPane.PaneId = PaneId.Left;

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);

        var targetEntry = vm.LeftPane.Items.First(i => i.Name == "old.txt");
        vm.LeftPane.CurrentItem = targetEntry;

        await vm.RenameCommand.ExecuteAsync(null);

        await Assert.That(targetEntry.EditBuffer).IsEqualTo("old.txt");
    }
}
