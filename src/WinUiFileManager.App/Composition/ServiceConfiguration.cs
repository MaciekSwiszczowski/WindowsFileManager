using System.Diagnostics;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using WinUiFileManager.App.Startup;
using WinUiFileManager.App.Windows;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Dialogs;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Diagnostics;
using WinUiFileManager.Infrastructure;
using WinUiFileManager.Presentation;
using WinUiFileManager.Presentation.MessageLogging;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.App.Composition;

public static class ServiceConfiguration
{
    public static AutofacServiceProvider ConfigureServices()
    {
        var builder = new ContainerBuilder();

        var services = new ServiceCollection();
        services.AddLogging();
        builder.Populate(services);

        builder.AddInfrastructureServices();
        builder.AddDiagnosticsServices();
        builder.AddPresentationServices();

        builder.RegisterType<PanelNavigationService>().SingleInstance();

        builder.RegisterType<SetParallelExecutionCommandHandler>().SingleInstance();
        builder.RegisterType<PersistPaneStateCommandHandler>().SingleInstance();

        builder.RegisterType<MessageLogStore>().SingleInstance();

        builder.RegisterType<DialogService>().SingleInstance();
        builder.RegisterType<WinUiClipboardService>().As<IClipboardService>().SingleInstance();

        builder.RegisterType<MessageDialogViewModel>().InstancePerDependency();
        builder.RegisterType<RenameDialogViewModel>().InstancePerDependency();

        builder.RegisterType<StartupChain>().SingleInstance();
        builder.RegisterType<StartupChainRunner>().SingleInstance();
        builder.RegisterType<StartupPathResolver>().SingleInstance();
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
