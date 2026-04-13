using WinUiFileManager.Application.Settings;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeSettingsRepository : ISettingsRepository
{
    public AppSettings Current { get; set; } = new();

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
        => Task.FromResult(Current);

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Current = settings;
        return Task.CompletedTask;
    }
}
