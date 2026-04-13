using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;

namespace WinUiFileManager.Application.Settings;

public sealed class SetParallelExecutionCommandHandler
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<SetParallelExecutionCommandHandler> _logger;

    public SetParallelExecutionCommandHandler(
        ISettingsRepository settingsRepository,
        ILogger<SetParallelExecutionCommandHandler> logger)
    {
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync(bool enabled, int maxDegreeOfParallelism, CancellationToken ct)
    {
        var settings = await _settingsRepository.LoadAsync(ct);

        var updated = settings with
        {
            ParallelExecutionEnabled = enabled,
            MaxDegreeOfParallelism = maxDegreeOfParallelism
        };

        await _settingsRepository.SaveAsync(updated, ct);

        _logger.LogInformation(
            "Parallel execution settings updated: Enabled={Enabled}, MaxDegree={MaxDegree}",
            enabled,
            maxDegreeOfParallelism);
    }
}
