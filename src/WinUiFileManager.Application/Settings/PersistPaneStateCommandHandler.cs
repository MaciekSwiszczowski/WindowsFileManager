using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;

namespace WinUiFileManager.Application.Settings;

/// <summary>
/// Command handler that persists the current dual-pane UI state (pane paths, active pane, inspector
/// visibility/width, pane width, column layouts, sort, window placement) into <see cref="AppSettings"/>.
/// Typically invoked on shutdown / state-changing UI events.
/// </summary>
/// <remarks>
/// The load-modify-write in <see cref="ExecuteAsync"/> is <b>non-transactional (TOCTOU)</b>: it reads the
/// whole document, mutates a subset of fields, and writes it back. The current
/// <see cref="WinUiFileManager.Application.Abstractions.ISettingsRepository"/> API has no partial update or
/// concurrency token, so a concurrent writer (e.g. <see cref="SetParallelExecutionCommandHandler"/>) between
/// load and save can be clobbered. Acceptable today because these writes are effectively serialized by the UI.
/// </remarks>
public sealed class PersistPaneStateCommandHandler
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<PersistPaneStateCommandHandler> _logger;

    public PersistPaneStateCommandHandler(
        ISettingsRepository settingsRepository,
        ILogger<PersistPaneStateCommandHandler> logger)
    {
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Loads the current settings, overlays the pane-state fields from <paramref name="request"/>, and
    /// saves the result. See the TOCTOU note on the type — load and save are not atomic.
    /// </summary>
    /// <param name="request">The pane-state values to persist.</param>
    /// <param name="ct">Cancels both the load and the save.</param>
    public async Task ExecuteAsync(PersistPaneStateRequest request, CancellationToken ct)
    {
        var current = await _settingsRepository.LoadAsync(ct).ConfigureAwait(false);

        var updated = current with
        {
            LastLeftPanePath = request.LeftPanePath,
            LastRightPanePath = request.RightPanePath,
            LastActivePane = request.ActivePane,
            InspectorVisible = request.InspectorVisible,
            InspectorWidth = request.InspectorWidth,
            LeftPaneWidth = request.LeftPaneWidth,
            LeftPaneColumns = request.LeftPaneColumns,
            RightPaneColumns = request.RightPaneColumns,
            LeftPaneSort = request.LeftPaneSort,
            RightPaneSort = request.RightPaneSort,
            MainWindowPlacement = request.MainWindowPlacement
        };

        await _settingsRepository.SaveAsync(updated, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Pane state persisted: Left={Left}, Right={Right}, Active={Active}, InspectorVisible={InspectorVisible}, InspectorWidth={InspectorWidth}, LeftPaneWidth={LeftPaneWidth}, Placement={Placement}",
            request.LeftPanePath?.DisplayPath,
            request.RightPanePath?.DisplayPath,
            request.ActivePane,
            request.InspectorVisible,
            request.InspectorWidth,
            request.LeftPaneWidth,
            request.MainWindowPlacement);
    }
}
