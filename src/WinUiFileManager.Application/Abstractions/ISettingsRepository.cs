using WinUiFileManager.Application.Settings;

namespace WinUiFileManager.Application.Abstractions;

/// <summary>
/// Persists and retrieves the full <see cref="AppSettings"/> snapshot. Implemented in Infrastructure
/// (e.g. a JSON file on disk).
/// </summary>
/// <remarks>
/// The API is whole-document load/save with no partial update or optimistic-concurrency token.
/// Command handlers that do load-modify-write (e.g.
/// <see cref="WinUiFileManager.Application.Settings.PersistPaneStateCommandHandler"/>,
/// <see cref="WinUiFileManager.Application.Settings.SetParallelExecutionCommandHandler"/>) are therefore
/// <b>non-transactional</b>: a concurrent writer between <see cref="LoadAsync"/> and <see cref="SaveAsync"/>
/// can have its changes clobbered (a TOCTOU race).
/// </remarks>
public interface ISettingsRepository
{
    /// <summary>Loads the current settings, returning defaults when none are persisted yet.</summary>
    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Overwrites the persisted settings with <paramref name="settings"/> in full.</summary>
    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
