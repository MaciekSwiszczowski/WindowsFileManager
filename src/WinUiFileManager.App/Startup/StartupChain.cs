namespace WinUiFileManager.App.Startup;

using CommunityToolkit.Mvvm.Messaging;
using Application.Abstractions;
using Application.Navigation;
using Application.Startup;
using Diagnostics.FileOperations;
using Infrastructure.Services;

public sealed class StartupChain
{
    private readonly ActivePanelsService _activePanelsService;
    private readonly FileOperationRequestHandler _fileOperationRequestHandler;
    private readonly IMessenger _messenger;
    private readonly PanelNavigationService _panelNavigationService;
    private readonly ISettingsRepository _settingsRepository;
    private readonly RenameService _renameService;
    private readonly INtfsVolumePolicyService _volumePolicyService;

    public StartupChain(
        ActivePanelsService activePanelsService,
        FileOperationRequestHandler fileOperationRequestHandler,
        IMessenger messenger,
        PanelNavigationService panelNavigationService,
        ISettingsRepository settingsRepository,
        RenameService renameService,
        INtfsVolumePolicyService volumePolicyService)
    {
        _activePanelsService = activePanelsService;
        _fileOperationRequestHandler = fileOperationRequestHandler;
        _messenger = messenger;
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

        var settingsTask = _settingsRepository.LoadAsync(cancellationToken);
        var volumesTask = _volumePolicyService.GetNtfsVolumesAsync(cancellationToken);

        await Task.WhenAll(settingsTask, volumesTask).ConfigureAwait(false);

        var startupData = new AppStartupData(
            await settingsTask.ConfigureAwait(false),
            await volumesTask.ConfigureAwait(false));
        _messenger.Send(new AppStartupDataLoadedMessage(startupData));
    }
}
