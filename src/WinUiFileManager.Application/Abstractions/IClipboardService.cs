namespace WinUiFileManager.Application.Abstractions;

/// <summary>
/// Abstraction over the system clipboard. Implemented in Infrastructure (clipboard APIs are UI/STA-bound,
/// so the implementation marshals appropriately); used e.g. by the "copy path" command.
/// </summary>
public interface IClipboardService
{
    /// <summary>Places <paramref name="text"/> on the clipboard.</summary>
    /// <param name="text">The text to copy.</param>
    /// <param name="ct">Cancels the operation.</param>
    Task SetTextAsync(string text, CancellationToken ct);
}
