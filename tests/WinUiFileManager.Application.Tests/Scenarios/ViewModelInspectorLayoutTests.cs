using System.Reflection;
using System.Runtime.CompilerServices;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelInspectorLayoutTests
{
    [Fact]
    public void InspectorColumnWidth_CollapsesWhenInspectorHidden()
    {
        var vm = CreateLayoutHarness();
        vm.InspectorWidth = 412d;

        SetInspectorVisible(vm, false);

        Assert.Equal(0d, vm.InspectorColumnWidth);
        Assert.Equal(0d, vm.InspectorMinWidth);
    }

    [Fact]
    public void InspectorColumnWidth_UsesMinimumWhenVisible()
    {
        var vm = CreateLayoutHarness();
        vm.InspectorWidth = 100d;
        SetInspectorVisible(vm, true);

        Assert.Equal(260d, vm.InspectorColumnWidth);
        Assert.Equal(260d, vm.InspectorMinWidth);
    }

    [Fact]
    public void UpdateInspectorWidthFromLayout_ClampsAndStoresVisibleWidth()
    {
        var vm = CreateLayoutHarness();
        SetInspectorVisible(vm, true);

        vm.UpdateInspectorWidthFromLayout(180d);

        Assert.Equal(260d, vm.InspectorWidth);
        Assert.Equal(260d, vm.InspectorColumnWidth);
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
