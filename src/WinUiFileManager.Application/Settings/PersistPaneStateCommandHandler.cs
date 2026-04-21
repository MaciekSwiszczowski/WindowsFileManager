using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;

namespace WinUiFileManager.Application.Settings;

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

    public async Task ExecuteAsync(PersistPaneStateRequest request, CancellationToken ct)
    {
        var current = await _settingsRepository.LoadAsync(ct);

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

        await _settingsRepository.SaveAsync(updated, ct);

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
