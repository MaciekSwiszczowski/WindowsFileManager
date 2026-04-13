namespace WinUiFileManager.Application.Abstractions;

public interface IClipboardService
{
    Task SetTextAsync(string text, CancellationToken ct);
}
