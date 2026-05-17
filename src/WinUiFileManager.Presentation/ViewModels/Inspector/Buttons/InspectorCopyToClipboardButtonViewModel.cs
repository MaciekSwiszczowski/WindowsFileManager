using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;

public sealed class InspectorCopyToClipboardButtonViewModel
{
    private readonly IClipboardService _clipboardService;
    private Func<IReadOnlyList<InspectorCategoryViewModel>> _categories = static () => [];

    public InspectorCopyToClipboardButtonViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
        CopyToClipboardCommand = new AsyncRelayCommand(CopyToClipboardAsync);
    }

    public IAsyncRelayCommand CopyToClipboardCommand { get; }

    public void Initialize(Func<IReadOnlyList<InspectorCategoryViewModel>> categories)
    {
        _categories = categories;
    }

    private async Task CopyToClipboardAsync()
    {
        var builder = new StringBuilder();
        foreach (var category in _categories().Where(static category => category.HasVisibleFields))
        {
            builder.AppendLine(category.Name);
            foreach (var field in category.Fields.Where(static field => field.IsVisible))
            {
                builder.Append("  ").Append(field.Key).Append(": ").AppendLine(GetCopyValue(field));
            }

            builder.AppendLine();
        }

        var text = builder.ToString().TrimEnd();
        if (!string.IsNullOrWhiteSpace(text))
        {
            await _clipboardService.SetTextAsync(text, CancellationToken.None);
        }
    }

    private static string GetCopyValue(InspectorFieldViewModel field) =>
        field.DisplayValue;
}
