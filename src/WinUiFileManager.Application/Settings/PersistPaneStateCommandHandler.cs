using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

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

    public async Task ExecuteAsync(
        NormalizedPath? leftPanePath,
        NormalizedPath? rightPanePath,
        PaneId activePane,
        CancellationToken ct)
    {
        var current = await _settingsRepository.LoadAsync(ct);

        var updated = current with
        {
            LastLeftPanePath = leftPanePath,
            LastRightPanePath = rightPanePath,
            LastActivePane = activePane
        };

        await _settingsRepository.SaveAsync(updated, ct);

        _logger.LogInformation(
            "Pane state persisted: Left={Left}, Right={Right}, Active={Active}",
            leftPanePath?.DisplayPath,
            rightPanePath?.DisplayPath,
            activePane);
    }
}
