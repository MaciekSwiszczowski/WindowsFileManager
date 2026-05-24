using System.Diagnostics;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using WinUiFileManager.App.Windows;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Diagnostics;
using WinUiFileManager.Infrastructure;
using WinUiFileManager.Presentation.FileEntryTableData;
using WinUiFileManager.Presentation.MessageLogging;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels;
using WinUiFileManager.Presentation.ViewModels.Inspector;
using WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;
using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;
using WinUiFileManager.Presentation.ViewModels.Inspector.Search;

namespace WinUiFileManager.App.Composition;

public static class ServiceConfiguration
{
    public static AutofacServiceProvider ConfigureServices()
    {
        var builder = new ContainerBuilder();

        var services = new ServiceCollection();
        services.AddLogging();
        builder.Populate(services);

        builder.RegisterInstance(new FileInspectorInteropOptions(FileInspectorInteropCategories.All));

        builder.AddInfrastructureServices();
        builder.AddDiagnosticsServices();
        builder.AddFileEntryTableDataServices();

        builder.RegisterType<PanelNavigationService>().SingleInstance();

        builder.RegisterType<SetParallelExecutionCommandHandler>().SingleInstance();
        builder.RegisterType<PersistPaneStateCommandHandler>().SingleInstance();

        builder.RegisterType<MessageLogStore>().SingleInstance();

        builder.RegisterType<DialogService>().SingleInstance();
        builder.RegisterType<WinUiClipboardService>().As<IClipboardService>().SingleInstance();

        builder.RegisterType<AppInitializationViewModel>().InstancePerDependency();
        builder.RegisterType<PanelsViewModel>().InstancePerDependency();
        builder.RegisterType<CommandButtonsViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorInitializationViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorRefreshButtonViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorPropertiesButtonViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorCopyToClipboardButtonViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorSearchViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorAttributeToggleViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorViewModel>().InstancePerDependency();
        builder.RegisterType<MainShellViewModel>().InstancePerDependency();
        builder.RegisterType<StatusBarViewModel>().InstancePerDependency();

        builder.RegisterType<MainShellWindow>().InstancePerDependency();

        var container = builder.Build();
        ApplyDevelopmentContainerValidation(container);
        return new AutofacServiceProvider(container);
    }

    [Conditional("DEBUG_ANALYZERS")]
    private static void ApplyDevelopmentContainerValidation(IContainer container)
    {
        container.Resolve<MainShellViewModel>();
    }
}
