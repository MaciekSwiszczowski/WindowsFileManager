using Autofac;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels.Inspector;
using WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;
using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;
using WinUiFileManager.Presentation.ViewModels.Inspector.Search;
using WinUiFileManager.Presentation.ViewModels.Panels;

namespace WinUiFileManager.Presentation;

public static class PresentationContainerBuilderExtensions
{
    public static void AddPresentationServices(this ContainerBuilder builder)
    {
        builder.RegisterInstance(FileEntryDisplayStringCache.Shared).SingleInstance();
        builder.RegisterType<FileEntryRowFactory>().SingleInstance();
        builder.RegisterType<FileEntryTableDataSource>().InstancePerDependency();
        builder.RegisterType<WindowsFileEntryRowReader>().As<IFileEntryRowReader>().SingleInstance();
        builder.RegisterType<WindowsFolderEntryScanner>().As<IFolderEntryScanner>().SingleInstance();
        builder.AddPresentationViewModels();
    }

    private static void AddPresentationViewModels(this ContainerBuilder builder)
    {
        builder.RegisterType<AppInitializationViewModel>().SingleInstance();
        builder.RegisterType<PanelsViewModel>().SingleInstance();
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
        builder.RegisterType<InspectorStreamsDeferredFieldLoader>().As<IInspectorDeferredFieldLoader>().InstancePerDependency();
        builder.RegisterType<InspectorCategoryViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorBasicFieldViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorThumbnailFieldViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorToggleFieldViewModel>().InstancePerDependency();

        builder.RegisterType<SpecFileEntryViewModel>().InstancePerDependency();
    }
}
