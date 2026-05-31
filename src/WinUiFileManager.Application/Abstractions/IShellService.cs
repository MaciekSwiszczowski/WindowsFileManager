using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Application.Abstractions;

/// <summary>
/// Wraps Windows Shell verbs (open-with-default-app, file properties dialog). Implemented in
/// Infrastructure via the Interop layer; these invoke shell UI and are UI/STA-affine.
/// </summary>
public interface IShellService
{
    /// <summary>Launches <paramref name="path"/> with its registered default application.</summary>
    /// <param name="ct">Cancels the launch request.</param>
    Task OpenWithDefaultAppAsync(NormalizedPath path, CancellationToken ct);

    /// <summary>Opens the Windows file-properties dialog for <paramref name="path"/>.</summary>
    /// <param name="ct">Cancels the request.</param>
    Task ShowPropertiesAsync(NormalizedPath path, CancellationToken ct);
}

