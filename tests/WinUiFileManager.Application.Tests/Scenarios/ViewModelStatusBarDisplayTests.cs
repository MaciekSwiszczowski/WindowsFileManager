namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelStatusBarDisplayTests
{
    [Test]
    public async Task Test_FilePane_ItemCountDisplay_AppendsIncrementalSearch()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        fixture.CreateFile("source/alpha.txt", sizeInBytes: 32);
        fixture.CreateFile("source/beta.txt", sizeInBytes: 16);

        var builder = new ViewModelTestBuilder();
        using var vm = builder.Build();

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);

        await Assert.That(vm.LeftPane.ItemCountDisplay).IsEqualTo("2 items");

        vm.LeftPane.HandleIncrementalSearch('a');

        await Assert.That(vm.LeftPane.ItemCountDisplay).IsEqualTo("2 items | Search: a");
    }

    [Test]
    public async Task Test_FilePane_SelectedDisplay_IncludesAggregateBytes()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        fixture.CreateFile("source/alpha.txt", sizeInBytes: 10);
        fixture.CreateFile("source/beta.txt", sizeInBytes: 20);

        var builder = new ViewModelTestBuilder();
        using var vm = builder.Build();

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);

        vm.LeftPane.UpdateSelectionFromControl([
            vm.LeftPane.Items.Single(item => item.Name == "alpha.txt"),
            vm.LeftPane.Items.Single(item => item.Name == "beta.txt")
        ]);

        await Assert.That(vm.LeftPane.SelectedDisplay).IsEqualTo("2 selected (30 B)");
    }

    [Test]
    public async Task Test_MainShell_ActivePaneLabel_FollowsActivePane()
    {
        var builder = new ViewModelTestBuilder();
        using var vm = builder.Build();

        await Assert.That(vm.ActivePaneLabel).IsEqualTo("Left active");

        vm.ActivePane = vm.RightPane;

        await Assert.That(vm.ActivePaneLabel).IsEqualTo("Right active");
    }
}
