namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeClipboardService : IClipboardService
{
    public string? LastCopiedText { get; private set; }

    public Task SetTextAsync(string text, CancellationToken ct)
    {
        LastCopiedText = text;
        return Task.CompletedTask;
    }
}
