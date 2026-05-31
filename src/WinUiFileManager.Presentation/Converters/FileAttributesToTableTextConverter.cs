using Microsoft.UI.Xaml.Data;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.Converters;

/// <summary>
/// One-way XAML value converter turning a <see cref="FileAttributes"/> flags value into the compact
/// attribute string shown in the file table's Attributes column (e.g. "RHA").
/// </summary>
/// <remarks>
/// Input: <see cref="FileAttributes"/> → output: <see cref="string"/>. Formatting is delegated to the
/// process-wide <see cref="FileEntryDisplayStringCache.Shared"/> so the same attribute string is
/// memoised across every row rather than re-built per cell (AGENTS.md §3). Used directly in the cell
/// template because the converter must run for each virtualised row on demand. One-way only.
/// </remarks>
public sealed class FileAttributesToTableTextConverter : IValueConverter
{
    /// <summary>Returns the cached attribute display text for a <see cref="FileAttributes"/> value;
    /// empty for any other input.</summary>
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is FileAttributes attributes
            ? FileEntryDisplayStringCache.Shared.GetTableAttributes(attributes)
            : string.Empty;

    /// <summary>Not supported — this is a display-only, one-way conversion.</summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
