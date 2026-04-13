using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Properties;

public sealed class ShowPropertiesCommandHandler
{
    private readonly IDialogService _dialogService;

    public ShowPropertiesCommandHandler(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public async Task ExecuteAsync(IReadOnlyList<FileSystemEntryModel> entries, CancellationToken ct)
    {
        await _dialogService.ShowPropertiesAsync(entries, ct);
    }
}
