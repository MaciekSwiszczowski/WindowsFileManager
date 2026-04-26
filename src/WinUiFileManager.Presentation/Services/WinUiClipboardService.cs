using Windows.ApplicationModel.DataTransfer;

namespace WinUiFileManager.Presentation.Services;

public sealed class WinUiClipboardService : IClipboardService
{
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
