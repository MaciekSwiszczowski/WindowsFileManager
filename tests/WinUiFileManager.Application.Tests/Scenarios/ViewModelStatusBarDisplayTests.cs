namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelStatusBarDisplayTests
{
    [Test]
    public async Task Test_MainShell_ActivePaneLabel_FollowsActivePane()
    {
        var builder = new ViewModelTestBuilder();
        using var vm = builder.Build();

        await Assert.That(vm.ActivePaneLabel).IsEqualTo("Left active");

        vm.ActivePaneId = PaneId.Right;

        await Assert.That(vm.ActivePaneLabel).IsEqualTo("Right active");
    }
}
