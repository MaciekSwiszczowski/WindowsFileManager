using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using WinUiFileManager.App.Windows;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Favourites;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Infrastructure;
using WinUiFileManager.Presentation.MessageLogging;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels;
using WinUiFileManager.Presentation.ViewModels.Inspector;

namespace WinUiFileManager.App.Composition;

public static class ServiceConfiguration
{
    public static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(new FileInspectorInteropOptions(
            FileInspectorInteropCategories.All));

        services.AddInfrastructureServices();

        services.AddSingleton<GoToPathCommandHandler>();
        services.AddSingleton<PanelNavigationService>();

        services.AddSingleton<AddFavouriteCommandHandler>();
        services.AddSingleton<RemoveFavouriteCommandHandler>();
        services.AddSingleton<OpenFavouriteCommandHandler>();

        services.AddSingleton<SetParallelExecutionCommandHandler>();
        services.AddSingleton<PersistPaneStateCommandHandler>();

        services.AddSingleton<MessageLogStore>();

        services.AddSingleton<DialogService>();
        services.AddSingleton<IClipboardService, WinUiClipboardService>();

        services.AddTransient<AppInitializationViewModel>();
        services.AddTransient<PanelsViewModel>();
        services.AddTransient<CommandButtonsViewModel>();
        services.AddTransient<InspectorViewModel>();
        services.AddTransient<MainShellViewModel>();
        services.AddTransient<StatusBarViewModel>();

        services.AddTransient<MainShellWindow>();

        var options = new ServiceProviderOptions();
        ApplyDevelopmentServiceProviderValidation(options);
        return services.BuildServiceProvider(options);
    }

    [Conditional("DEBUG")]
    private static void ApplyDevelopmentServiceProviderValidation(ServiceProviderOptions options)
    {
        options.ValidateOnBuild = true;
        options.ValidateScopes = true;
    }
}
