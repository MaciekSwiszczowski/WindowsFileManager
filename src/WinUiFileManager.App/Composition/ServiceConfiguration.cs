using Microsoft.Extensions.DependencyInjection;
using WinUiFileManager.App.Windows;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Favourites;
using WinUiFileManager.Application.FileOperations;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Infrastructure;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels;

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

        services.AddSingleton<OpenEntryCommandHandler>();
        services.AddSingleton<NavigateUpCommandHandler>();
        services.AddSingleton<GoToPathCommandHandler>();
        services.AddSingleton<RefreshPaneCommandHandler>();

        services.AddSingleton<CopySelectionCommandHandler>();
        services.AddSingleton<MoveSelectionCommandHandler>();
        services.AddSingleton<DeleteSelectionCommandHandler>();
        services.AddSingleton<CreateFolderCommandHandler>();
        services.AddSingleton<RenameEntryCommandHandler>();
        services.AddSingleton<CopyFullPathCommandHandler>();

        services.AddSingleton<AddFavouriteCommandHandler>();
        services.AddSingleton<RemoveFavouriteCommandHandler>();
        services.AddSingleton<OpenFavouriteCommandHandler>();

        services.AddSingleton<SetParallelExecutionCommandHandler>();
        services.AddSingleton<PersistPaneStateCommandHandler>();

        services.AddSingleton<WinUiDialogService>();
        services.AddSingleton<IDialogService>(static sp => sp.GetRequiredService<WinUiDialogService>());
        services.AddSingleton<IClipboardService, WinUiClipboardService>();
        services.AddSingleton<FileTableFocusService>();

        services.AddTransient<FilePaneViewModel>();
        services.AddTransient<FileInspectorViewModel>();
        services.AddTransient<MainShellViewModel>();
        services.AddTransient<StatusBarViewModel>();

        services.AddTransient<MainShellWindow>();

#if DEBUG
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
#else
        return services.BuildServiceProvider();
#endif
    }
}
