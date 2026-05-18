using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using WinUiFileManager.App.Windows;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Diagnostics;
using WinUiFileManager.Infrastructure;
using WinUiFileManager.Presentation.FileEntryTable.Data;
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
    public static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(new FileInspectorInteropOptions(FileInspectorInteropCategories.All));

        services.AddInfrastructureServices();
        services.AddDiagnosticsServices();

        services.AddSingleton<PanelNavigationService>();

        services.AddSingleton<SetParallelExecutionCommandHandler>();
        services.AddSingleton<PersistPaneStateCommandHandler>();

        services.AddSingleton<MessageLogStore>();

        services.AddSingleton<DialogService>();
        services.AddSingleton<IClipboardService, WinUiClipboardService>();

        services.AddTransient<AppInitializationViewModel>();
        services.AddSingleton<FileEntryDataReader>();
        services.AddTransient<PanelsViewModel>();
        services.AddTransient<CommandButtonsViewModel>();
        services.AddTransient<InspectorInitializationViewModel>();
        services.AddTransient<InspectorRefreshButtonViewModel>();
        services.AddTransient<InspectorPropertiesButtonViewModel>();
        services.AddTransient<InspectorCopyToClipboardButtonViewModel>();
        services.AddTransient<InspectorSearchViewModel>();
        services.AddTransient<InspectorAttributeToggleViewModel>();
        services.AddTransient<InspectorViewModel>();
        services.AddTransient<MainShellViewModel>();
        services.AddTransient<StatusBarViewModel>();

        services.AddTransient<MainShellWindow>();

        var options = new ServiceProviderOptions();
        ApplyDevelopmentServiceProviderValidation(options);
        return services.BuildServiceProvider(options);
    }

    [Conditional("DEBUG_ANALYZERS")]
    private static void ApplyDevelopmentServiceProviderValidation(ServiceProviderOptions options)
    {
        options.ValidateOnBuild = true;
        options.ValidateScopes = true;
    }
}
