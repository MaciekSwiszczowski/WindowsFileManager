using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.FileOperations;

public sealed class CopyFullPathCommandHandler
{
    private readonly IClipboardService _clipboardService;

    public CopyFullPathCommandHandler(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public async Task ExecuteAsync(IReadOnlyList<FileSystemEntryModel> entries, CancellationToken ct)
    {
        var text = string.Join(Environment.NewLine, entries.Select(e => e.FullPath.DisplayPath));
        await _clipboardService.SetTextAsync(text, ct);
    }
}
