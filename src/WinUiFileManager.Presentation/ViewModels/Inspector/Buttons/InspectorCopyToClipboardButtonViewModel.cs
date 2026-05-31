using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;

/// <summary>
/// View model for the inspector's "Copy to clipboard" button. Serializes the currently visible categories/fields
/// into an indented plain-text block and writes it via <see cref="IClipboardService"/>. Only visible categories
/// and fields are included, so the copied text mirrors the current search-filtered view.
/// </summary>
/// <remarks>
/// The categories are supplied lazily through <see cref="Initialize"/> (a func, defaulting to empty) so the
/// button can be constructed before the inspector's category list exists.
/// </remarks>
public sealed class InspectorCopyToClipboardButtonViewModel
{
    private readonly IClipboardService _clipboardService;
    private Func<IReadOnlyList<InspectorCategoryViewModel>> _categories = static () => [];

    public InspectorCopyToClipboardButtonViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
        CopyToClipboardCommand = new AsyncRelayCommand(CopyToClipboardAsync);
    }

    /// <summary>Command bound to the copy button.</summary>
    public IAsyncRelayCommand CopyToClipboardCommand { get; }

    /// <summary>Supplies the live category list to serialize; called by the inspector during wiring.</summary>
    public void Initialize(Func<IReadOnlyList<InspectorCategoryViewModel>> categories)
    {
        _categories = categories;
    }

    /// <summary>
    /// Builds the indented text snapshot of visible fields and copies it to the clipboard. Skips the copy when the
    /// result is blank. The clipboard write is UI/STA-affine.
    /// </summary>
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

    /// <summary>Returns the text used to represent a field in the clipboard output.</summary>
    private static string GetCopyValue(InspectorFieldViewModelBase field) =>
        field.DisplayValue;
}
