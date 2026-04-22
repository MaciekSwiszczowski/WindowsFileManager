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
        using var vm = builder.Build();
        vm.LeftPane.PaneId = PaneId.Left;

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);

        var targetEntry = vm.LeftPane.Items.First(i => i.Name == "old.txt");
        vm.LeftPane.CurrentItem = targetEntry;

        await vm.RenameCommand.ExecuteAsync(null);

        await Assert.That(targetEntry.EditBuffer).IsEqualTo("old.txt");
        await Assert.That(targetEntry.IsEditing).IsTrue();
        await Assert.That(vm.LeftPane.ActiveEditingEntry).IsSameReferenceAs(targetEntry);
    }

    [Test]
    public async Task Test_CommitRenameAsync_RenamesFile()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        var originalPath = fixture.CreateFile("source/old.txt", sizeInBytes: 32);

        var builder = new ViewModelTestBuilder();
        using var vm = builder.Build();
        vm.LeftPane.PaneId = PaneId.Left;

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);

        var targetEntry = vm.LeftPane.Items.First(i => i.Name == "old.txt");
        vm.LeftPane.CurrentItem = targetEntry;
        vm.LeftPane.BeginRenameCurrent();

        var committed = await vm.LeftPane.CommitRenameAsync(targetEntry, "new.txt", CancellationToken.None);

        await Assert.That(committed).IsTrue();
        await Assert.That(targetEntry.IsEditing).IsFalse();
        await Assert.That(File.Exists(Path.Combine(sourceDir, "new.txt"))).IsTrue();
        await Assert.That(File.Exists(originalPath)).IsFalse();
    }

    [Test]
    public async Task Test_CommitRenameAsync_CollisionKeepsEditOpen()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        fixture.CreateFile("source/old.txt", sizeInBytes: 32);
        fixture.CreateFile("source/existing.txt", sizeInBytes: 16);

        var builder = new ViewModelTestBuilder();
        using var vm = builder.Build();
        vm.LeftPane.PaneId = PaneId.Left;

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);

        var targetEntry = vm.LeftPane.Items.First(i => i.Name == "old.txt");
        vm.LeftPane.CurrentItem = targetEntry;
        vm.LeftPane.BeginRenameCurrent();

        var committed = await vm.LeftPane.CommitRenameAsync(targetEntry, "existing.txt", CancellationToken.None);

        await Assert.That(committed).IsFalse();
        await Assert.That(targetEntry.IsEditing).IsTrue();
        await Assert.That(vm.LeftPane.ActiveEditingEntry).IsSameReferenceAs(targetEntry);
        await Assert.That(vm.LeftPane.ErrorMessage).IsNull();
        await Assert.That(File.Exists(Path.Combine(sourceDir, "old.txt"))).IsTrue();
    }

    [Test]
    public async Task Test_ChangingCurrentItem_CancelsActiveRename()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        fixture.CreateFile("source/old.txt", sizeInBytes: 32);
        fixture.CreateFile("source/other.txt", sizeInBytes: 16);

        var builder = new ViewModelTestBuilder();
        using var vm = builder.Build();
        vm.LeftPane.PaneId = PaneId.Left;

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);

        var targetEntry = vm.LeftPane.Items.First(i => i.Name == "old.txt");
        var otherEntry = vm.LeftPane.Items.First(i => i.Name == "other.txt");
        vm.LeftPane.CurrentItem = targetEntry;
        vm.LeftPane.BeginRenameCurrent();

        vm.LeftPane.CurrentItem = otherEntry;

        await Assert.That(targetEntry.IsEditing).IsFalse();
        await Assert.That(targetEntry.EditBuffer).IsEmpty();
        await Assert.That(vm.LeftPane.ActiveEditingEntry).IsNull();
    }
}
