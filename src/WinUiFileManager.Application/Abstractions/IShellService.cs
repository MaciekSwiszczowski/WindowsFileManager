using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Application.Abstractions;

public interface IShellService
{
    Task OpenWithDefaultAppAsync(NormalizedPath path, CancellationToken ct);

    Task ShowPropertiesAsync(NormalizedPath path, CancellationToken ct);
}

