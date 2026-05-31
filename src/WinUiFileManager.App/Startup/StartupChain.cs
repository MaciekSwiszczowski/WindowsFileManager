namespace WinUiFileManager.App.Startup;

using CommunityToolkit.Mvvm.Messaging;
using Application.Abstractions;
using Application.Messages.RequestMessages.Navigation;
using Application.Navigation;
using Application.Startup;
using Diagnostics.FileOperations;
using Diagnostics.Inspector;
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
    private readonly InspectorStreamsDiagnosticsHandler _inspectorStreamsDiagnosticsHandler;
    private readonly IMessenger _messenger;
    private readonly PanelsViewModel _panels;
    private readonly PanelNavigationService _panelNavigationService;
    private readonly StartupPathResolver _startupPathResolver;
    private readonly ISettingsRepository _settingsRepository;
    private readonly RenameService _renameService;
    private readonly INtfsVolumePolicyService _volumePolicyService;

    public StartupChain(
        ActivePanelsService activePanelsService,
        FileOperationRequestHandler fileOperationRequestHandler,
        InspectorStreamsDiagnosticsHandler inspectorStreamsDiagnosticsHandler,
        IMessenger messenger,
        PanelsViewModel panels,
        PanelNavigationService panelNavigationService,
        StartupPathResolver startupPathResolver,
        ISettingsRepository settingsRepository,
        RenameService renameService,
        INtfsVolumePolicyService volumePolicyService)
    {
        _activePanelsService = activePanelsService;
        _fileOperationRequestHandler = fileOperationRequestHandler;
        _inspectorStreamsDiagnosticsHandler = inspectorStreamsDiagnosticsHandler;
        _messenger = messenger;
        _panels = panels;
        _panelNavigationService = panelNavigationService;
        _startupPathResolver = startupPathResolver;
        _settingsRepository = settingsRepository;
        _renameService = renameService;
        _volumePolicyService = volumePolicyService;
    }

    public async Task StartupChainAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _activePanelsService.Initialize();
        _fileOperationRequestHandler.Initialize();
        _inspectorStreamsDiagnosticsHandler.Initialize();
        _panelNavigationService.Initialize();
        _renameService.Initialize();
        _panels.Initialize();

        var settingsTask = _settingsRepository.LoadAsync(cancellationToken);
        var volumesTask = _volumePolicyService.GetNtfsVolumesAsync(cancellationToken);

        await Task.WhenAll(settingsTask, volumesTask).ConfigureAwait(false);

        var settings = await settingsTask.ConfigureAwait(false);
        var volumes = await volumesTask.ConfigureAwait(false);
        var startupPaths = _startupPathResolver.Resolve(settings, volumes);
        var startupData = new AppStartupData(settings, volumes, startupPaths.LeftPath, startupPaths.RightPath);

        _messenger.Send(new AppStartupDataLoadedMessage(startupData));
        _messenger.Send(new FileTableNavigateToPathRequestedMessage("Left", startupData.LeftInitialPath));
        _messenger.Send(new FileTableNavigateToPathRequestedMessage("Right", startupData.RightInitialPath));
    }
}
