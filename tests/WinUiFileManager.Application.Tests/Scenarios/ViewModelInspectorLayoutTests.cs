using System.Reflection;
using System.Runtime.CompilerServices;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelInspectorLayoutTests
{
    [Test]
    public async Task Test_InspectorColumnWidth_CollapsesWhenInspectorHidden()
    {
        var vm = CreateLayoutHarness();
        vm.InspectorWidth = 412d;

        SetInspectorVisible(vm, false);

        await Assert.That(vm.InspectorColumnWidth).IsEqualTo(0d);
        await Assert.That(vm.InspectorMinWidth).IsEqualTo(0d);
    }

    [Test]
    public async Task Test_InspectorColumnWidth_UsesMinimumWhenVisible()
    {
        var vm = CreateLayoutHarness();
        vm.InspectorWidth = 100d;
        SetInspectorVisible(vm, true);

        await Assert.That(vm.InspectorColumnWidth).IsEqualTo(260d);
        await Assert.That(vm.InspectorMinWidth).IsEqualTo(260d);
    }

    [Test]
    public async Task Test_UpdateInspectorWidthFromLayout_ClampsAndStoresVisibleWidth()
    {
        var vm = CreateLayoutHarness();
        SetInspectorVisible(vm, true);

        vm.UpdateInspectorWidthFromLayout(180d);

        await Assert.That(vm.InspectorWidth).IsEqualTo(260d);
        await Assert.That(vm.InspectorColumnWidth).IsEqualTo(260d);
    }

    private static MainShellViewModel CreateLayoutHarness()
    {
        var viewModel = RuntimeHelpers.GetUninitializedObject(typeof(MainShellViewModel));
        return (MainShellViewModel)viewModel;
    }

    private static void SetInspectorVisible(MainShellViewModel viewModel, bool value)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var field = typeof(MainShellViewModel).GetField("_isInspectorVisible", Flags);
        if (field is null)
        {
            throw new InvalidOperationException("MainShellViewModel layout field was not found.");
        }

        field.SetValue(viewModel, value);
    }
}
