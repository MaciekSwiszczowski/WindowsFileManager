using Windows.ApplicationModel.DataTransfer;

namespace WinUiFileManager.Presentation.Services;

/// <summary>
/// WinUI/WinRT implementation of <see cref="IClipboardService"/> backed by the system
/// <see cref="Clipboard"/>. The Presentation layer supplies this so the Application layer can copy text
/// (e.g. file paths) without referencing WinRT directly.
/// </summary>
/// <remarks>
/// WinRT clipboard APIs are STA/UI-thread affine (AGENTS.md §6); callers must invoke this on the UI
/// thread. <see cref="Clipboard.Flush"/> is called so the copied content survives after the app exits.
/// The method is synchronous under the hood but returns a completed <see cref="Task"/> to satisfy the
/// async contract (no fake-async wrapper of a long operation).
/// </remarks>
public sealed class WinUiClipboardService : IClipboardService
{
    /// <summary>Places <paramref name="text"/> on the system clipboard and flushes it so it persists.</summary>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="ct"/> is already cancelled.</exception>
    public Task SetTextAsync(string text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
        Clipboard.Flush();

        return Task.CompletedTask;
    }
}
