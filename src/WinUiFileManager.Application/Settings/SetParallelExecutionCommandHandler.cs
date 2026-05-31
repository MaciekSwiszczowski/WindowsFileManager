using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;

namespace WinUiFileManager.Application.Settings;

/// <summary>
/// Command handler that persists the parallel-execution preferences (enabled flag and max degree of
/// parallelism) into <see cref="AppSettings"/>.
/// </summary>
/// <remarks>
/// As with <see cref="PersistPaneStateCommandHandler"/>, the load-modify-write in <see cref="ExecuteAsync"/>
/// is <b>non-transactional (TOCTOU)</b> given the whole-document
/// <see cref="WinUiFileManager.Application.Abstractions.ISettingsRepository"/> API: a concurrent save between
/// load and save can overwrite the other writer's changes.
/// </remarks>
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

    /// <summary>
    /// Loads the current settings, updates the parallel-execution fields, and saves. See the TOCTOU note
    /// on the type — load and save are not atomic.
    /// </summary>
    /// <param name="enabled">Whether parallel execution is enabled.</param>
    /// <param name="maxDegreeOfParallelism">Maximum concurrent operations when enabled.</param>
    /// <param name="ct">Cancels both the load and the save.</param>
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
