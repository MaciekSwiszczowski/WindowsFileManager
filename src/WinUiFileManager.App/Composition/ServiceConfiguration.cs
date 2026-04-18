namespace WinUiFileManager.App.Composition;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Favourites;
using WinUiFileManager.Application.FileOperations;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Infrastructure;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels;

public static class ServiceConfiguration
{
    public static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();

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
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<WinUiDialogService>());
        services.AddSingleton<IClipboardService, WinUiClipboardService>();

        services.AddTransient<FilePaneViewModel>();
        services.AddTransient<FileInspectorViewModel>();
        services.AddTransient<MainShellViewModel>();
        services.AddTransient<StatusBarViewModel>();

        services.AddTransient<WinUiFileManager.App.Windows.MainShellWindow>();

        return services.BuildServiceProvider();
    }
}
