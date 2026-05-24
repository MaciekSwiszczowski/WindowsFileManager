using Autofac;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.ViewModels.Inspector;
using WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;
using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;
using WinUiFileManager.Presentation.ViewModels.Inspector.Search;
using WinUiFileManager.Presentation.ViewModels.Panels;

namespace WinUiFileManager.Presentation.ViewModels;

public static class ContainerBuilderExtensions
{
    public static void AddPresentationViewModels(this ContainerBuilder builder)
    {
        builder.RegisterType<AppInitializationViewModel>().InstancePerDependency();
        builder.RegisterType<PanelsViewModel>().InstancePerDependency();
        builder.RegisterType<PanelViewModel>().InstancePerDependency();
        builder.RegisterType<PanelFileEntryDataSourceViewModel>().InstancePerDependency();
        builder.RegisterType<CommandButtonsViewModel>().InstancePerDependency();
        builder.RegisterType<MainShellViewModel>().InstancePerDependency();
        builder.RegisterType<StatusBarViewModel>().InstancePerDependency();

        builder.RegisterType<InspectorInitializationViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorRefreshButtonViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorPropertiesButtonViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorCopyToClipboardButtonViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorSearchViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorAttributeToggleViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorCategoryViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorFieldViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorThumbnailFieldViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorToggleFieldViewModel>().InstancePerDependency();

        builder.RegisterType<FileInspectorViewModel>().InstancePerDependency();
        builder.RegisterType<FileInspectorCategoryViewModel>().InstancePerDependency();
        builder.RegisterType<FileInspectorFieldViewModel>().InstancePerDependency();

        builder.RegisterType<SpecFileEntryViewModel>().InstancePerDependency();

    }
}
