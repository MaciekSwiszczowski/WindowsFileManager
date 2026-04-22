namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelInspectorLayoutTests
{
    [Test]
    public async Task Test_InspectorColumnWidth_CollapsesWhenInspectorHidden()
    {
        var builder = new ViewModelTestBuilder();
        var vm = builder.Build();
        vm.InspectorWidth = 412d;

        vm.IsInspectorVisible = false;

        await Assert.That(vm.InspectorColumnWidth).IsEqualTo(0d);
        await Assert.That(vm.InspectorMinWidth).IsEqualTo(0d);
    }

    [Test]
    public async Task Test_InspectorColumnWidth_UsesMinimumWhenVisible()
    {
        var builder = new ViewModelTestBuilder();
        var vm = builder.Build();
        vm.InspectorWidth = 100d;
        vm.IsInspectorVisible = true;

        await Assert.That(vm.InspectorColumnWidth).IsEqualTo(260d);
        await Assert.That(vm.InspectorMinWidth).IsEqualTo(260d);
    }

    [Test]
    public async Task Test_UpdateInspectorWidthFromLayout_ClampsAndStoresVisibleWidth()
    {
        var builder = new ViewModelTestBuilder();
        var vm = builder.Build();

        vm.UpdateInspectorWidthFromLayout(180d);

        await Assert.That(vm.InspectorWidth).IsEqualTo(260d);
        await Assert.That(vm.InspectorColumnWidth).IsEqualTo(260d);
    }
}
