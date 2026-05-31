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
    private readonly InspectorCloudDiagnosticsHandler _inspectorCloudDiagnosticsHandler;
    private readonly InspectorIdentityDiagnosticsHandler _inspectorIdentityDiagnosticsHandler;
    private readonly InspectorLinksDiagnosticsHandler _inspectorLinksDiagnosticsHandler;
    private readonly InspectorLocksDiagnosticsHandler _inspectorLocksDiagnosticsHandler;
    private readonly InspectorSecurityDiagnosticsHandler _inspectorSecurityDiagnosticsHandler;
    private readonly InspectorStreamsDiagnosticsHandler _inspectorStreamsDiagnosticsHandler;
    private readonly InspectorThumbnailDiagnosticsHandler _inspectorThumbnailDiagnosticsHandler;
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
        InspectorCloudDiagnosticsHandler inspectorCloudDiagnosticsHandler,
        InspectorIdentityDiagnosticsHandler inspectorIdentityDiagnosticsHandler,
        InspectorLinksDiagnosticsHandler inspectorLinksDiagnosticsHandler,
        InspectorLocksDiagnosticsHandler inspectorLocksDiagnosticsHandler,
        InspectorSecurityDiagnosticsHandler inspectorSecurityDiagnosticsHandler,
        InspectorStreamsDiagnosticsHandler inspectorStreamsDiagnosticsHandler,
        InspectorThumbnailDiagnosticsHandler inspectorThumbnailDiagnosticsHandler,
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
        _inspectorCloudDiagnosticsHandler = inspectorCloudDiagnosticsHandler;
        _inspectorIdentityDiagnosticsHandler = inspectorIdentityDiagnosticsHandler;
        _inspectorLinksDiagnosticsHandler = inspectorLinksDiagnosticsHandler;
        _inspectorLocksDiagnosticsHandler = inspectorLocksDiagnosticsHandler;
        _inspectorSecurityDiagnosticsHandler = inspectorSecurityDiagnosticsHandler;
        _inspectorStreamsDiagnosticsHandler = inspectorStreamsDiagnosticsHandler;
        _inspectorThumbnailDiagnosticsHandler = inspectorThumbnailDiagnosticsHandler;
        _messenger = messenger;
        _panels = panels;
        _panelNavigationService = panelNavigationService;
        _startupPathResolver = startupPathResolver;
        _settingsRepository = settingsRepository;
        _renameService = renameService;
        _volumePolicyService = volumePolicyService;
    }

    /// <summary>
    /// Runs the one-shot startup sequence: register message handlers, load settings and volumes in
    /// parallel, resolve the initial pane paths, and broadcast the results.
    /// </summary>
    /// <param name="cancellationToken">Cancels startup; checked before work and flowed into the I/O loads.</param>
    /// <returns>A task that completes once the startup messages have been sent.</returns>
    /// <remarks>
    /// Threading: invoked from a thread-pool thread by <see cref="StartupChainRunner"/> and awaits with
    /// <c>ConfigureAwait(false)</c> (library-code convention, AGENTS.md §6); it must not touch WinUI
    /// objects directly. Recipients of the messages sent at the end are responsible for marshalling to
    /// the UI thread themselves.
    /// <para>
    /// <b>Ordering is significant.</b> All <c>Initialize()</c> calls run first so every recipient is
    /// registered with the messenger <i>before</i> the startup messages below are sent; otherwise the
    /// <see cref="AppStartupDataLoadedMessage"/> / navigation messages could be missed. These
    /// <c>Initialize()</c> calls are <b>not idempotent</b> on this type — there is no guard, so calling
    /// the chain twice would double-register handlers (and double-handle messages, AGENTS.md §4). The
    /// chain is therefore expected to run exactly once; <see cref="StartupChainRunner"/> enforces that.
    /// </para>
    /// </remarks>
    public async Task StartupChainAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Register all message recipients before any startup message is sent (see remarks: ordering +
        // non-idempotent Initialize).
        _activePanelsService.Initialize();
        _fileOperationRequestHandler.Initialize();
        _inspectorCloudDiagnosticsHandler.Initialize();
        _inspectorIdentityDiagnosticsHandler.Initialize();
        _inspectorLinksDiagnosticsHandler.Initialize();
        _inspectorLocksDiagnosticsHandler.Initialize();
        _inspectorSecurityDiagnosticsHandler.Initialize();
        _inspectorStreamsDiagnosticsHandler.Initialize();
        _inspectorThumbnailDiagnosticsHandler.Initialize();
        _panelNavigationService.Initialize();
        _renameService.Initialize();
        _panels.Initialize();

        // Settings and volume enumeration are independent, so run them concurrently to cut startup latency.
        var settingsTask = _settingsRepository.LoadAsync(cancellationToken);
        var volumesTask = _volumePolicyService.GetNtfsVolumesAsync(cancellationToken);

        await Task.WhenAll(settingsTask, volumesTask).ConfigureAwait(false);

        var settings = await settingsTask.ConfigureAwait(false);
        var volumes = await volumesTask.ConfigureAwait(false);
        var startupPaths = _startupPathResolver.Resolve(settings, volumes);
        var startupData = new AppStartupData(settings, volumes, startupPaths.LeftPath, startupPaths.RightPath);

        // Broadcast loaded data first, then trigger initial navigation per pane. Pane identity is passed
        // as the raw "Left"/"Right" literals the navigation messages key off.
        _messenger.Send(new AppStartupDataLoadedMessage(startupData));
        _messenger.Send(new FileTableNavigateToPathRequestedMessage("Left", startupData.LeftInitialPath));
        _messenger.Send(new FileTableNavigateToPathRequestedMessage("Right", startupData.RightInitialPath));
    }
}
