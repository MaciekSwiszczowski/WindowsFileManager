namespace WinUiFileManager.App.Startup;

using CommunityToolkit.Mvvm.Messaging;
using Application.Abstractions;
using Application.Navigation;
using Application.Startup;
using Diagnostics.FileOperations;
using Infrastructure.Services;
using Presentation.ViewModels;

/// <summary>
/// Runs application startup work that is allowed to execute from a background thread.
/// </summary>
/// <remarks>
/// Allowed here: initializing singleton services and view models that own their own UI dispatching,
/// starting background-safe I/O, and publishing startup results through messages.
/// Not allowed here: touching WinUI controls or XAML objects, accepting runtime view instances from
/// code-behind, creating alternate view model instances instead of using DI-owned singletons, blocking
/// the UI startup path, or mutating UI-bound state without dispatching inside the owning component.
/// </remarks>
public sealed class StartupChain
{
    private readonly ActivePanelsService _activePanelsService;
    private readonly FileOperationRequestHandler _fileOperationRequestHandler;
    private readonly IMessenger _messenger;
    private readonly PanelsViewModel _panels;
    private readonly PanelNavigationService _panelNavigationService;
    private readonly ISettingsRepository _settingsRepository;
    private readonly RenameService _renameService;
    private readonly INtfsVolumePolicyService _volumePolicyService;

    public StartupChain(
        ActivePanelsService activePanelsService,
        FileOperationRequestHandler fileOperationRequestHandler,
        IMessenger messenger,
        PanelsViewModel panels,
        PanelNavigationService panelNavigationService,
        ISettingsRepository settingsRepository,
        RenameService renameService,
        INtfsVolumePolicyService volumePolicyService)
    {
        _activePanelsService = activePanelsService;
        _fileOperationRequestHandler = fileOperationRequestHandler;
        _messenger = messenger;
        _panels = panels;
        _panelNavigationService = panelNavigationService;
        _settingsRepository = settingsRepository;
        _renameService = renameService;
        _volumePolicyService = volumePolicyService;
    }

    public async Task StartupChainAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _activePanelsService.Initialize();
        _fileOperationRequestHandler.Initialize();
        _panelNavigationService.Initialize();
        _renameService.Initialize();
        _panels.Initialize();

        var settingsTask = _settingsRepository.LoadAsync(cancellationToken);
        var volumesTask = _volumePolicyService.GetNtfsVolumesAsync(cancellationToken);

        await Task.WhenAll(settingsTask, volumesTask).ConfigureAwait(false);

        var startupData = new AppStartupData(await settingsTask, await volumesTask);
        _messenger.Send(new AppStartupDataLoadedMessage(startupData));
    }
}
