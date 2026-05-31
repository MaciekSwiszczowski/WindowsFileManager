using CommunityToolkit.WinUI.Converters;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

internal sealed class InspectorFieldValueUpdater
{
    private readonly FileSizeToFriendlyStringConverter _fileSizeConverter = new();
    private readonly FileEntryDisplayStringCache _displayStringCache;
    private readonly IReadOnlyDictionary<string, InspectorFieldViewModelBase> _fields;

    public InspectorFieldValueUpdater(
        IReadOnlyList<InspectorCategoryViewModel> categories,
        FileEntryDisplayStringCache displayStringCache)
    {
        _displayStringCache = displayStringCache;
        _fields = categories
            .SelectMany(static category => category.Fields)
            .ToDictionary(static field => field.Key, StringComparer.OrdinalIgnoreCase);
    }

    public void ShowImmediateSelection(SpecFileEntryViewModel selectedItem)
    {
        ClearValues();

        if (selectedItem.Model is not { } model)
        {
            return;
        }

        SetValue("Name", model.Name);
        SetValue("Full Path", model.FullPath.DisplayPath);
        SetValue("Type", model.Kind == ItemKind.Directory ? "Folder" : "File");
        SetValue("Extension", _displayStringCache.GetExtension(model.Extension));
        SetValue("Size", ConvertSize(model));
        SetValue("Attributes", _displayStringCache.GetInspectorAttributes(model.Attributes));

        SetValue("Created", FormatLocalTime(model.CreationTime));
        SetValue("Modified", FormatLocalTime(model.LastWriteTime));
        SetAttributeFlags(model.Attributes);
    }

    public void ShowStreamsDiagnostics(FileStreamDiagnosticsDetails diagnostics)
    {
        var streamCount = string.IsNullOrWhiteSpace(diagnostics.AlternateStreamCount)
            ? "0"
            : diagnostics.AlternateStreamCount;

        SetValue("Alternate Stream Count", streamCount);
        SetValue(
            "Alternate Streams",
            diagnostics.AlternateStreams.Count > 0
                ? string.Join(Environment.NewLine, diagnostics.AlternateStreams)
                : "No alternate streams");
    }

    public void SetLoading(IEnumerable<string> keys, bool isLoading)
    {
        foreach (var key in keys)
        {
            if (_fields.TryGetValue(key, out var field))
            {
                field.IsLoading = isLoading;
            }
        }
    }

    private void ClearValues()
    {
        foreach (var field in _fields.Values)
        {
            field.Value = string.Empty;
            field.IsLoading = false;
            field.IsVisible = true;

            if (field is InspectorThumbnailFieldViewModel thumbnailField)
            {
                thumbnailField.ThumbnailSource = null;
            }
        }
    }

    private void SetAttributeFlags(FileAttributes attributes)
    {
        SetValue("Read Only", FormatFlag(attributes.HasFlag(FileAttributes.ReadOnly)));
        SetValue("Hidden", FormatFlag(attributes.HasFlag(FileAttributes.Hidden)));
        SetValue("Archive", FormatFlag(attributes.HasFlag(FileAttributes.Archive)));
        SetValue("Encrypted", FormatFlag(attributes.HasFlag(FileAttributes.Encrypted)));
        SetValue("Compressed", FormatFlag(attributes.HasFlag(FileAttributes.Compressed)));
        SetValue("Reparse Point", FormatFlag(attributes.HasFlag(FileAttributes.ReparsePoint)));
    }

    private void SetValue(string key, string value)
    {
        if (_fields.TryGetValue(key, out var field))
        {
            field.Value = value;
        }
    }

    private static string FormatLocalTime(DateTime value) =>
        value == DateTime.MinValue
            ? string.Empty
            : value.ToString("yyyy-MM-dd HH:mm:ss");

    private static string FormatFlag(bool value) => value ? "Yes" : "No";

    private string ConvertSize(FileSystemEntryModel model)
    {
        if (model.Size is not { } size)
        {
            return string.Empty;
        }

        return _fileSizeConverter.Convert(size, typeof(string), string.Empty, string.Empty) as string
            ?? string.Empty;
    }
}
