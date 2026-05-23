using CommunityToolkit.WinUI.Converters;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

internal sealed class InspectorFieldValueUpdater
{
    private readonly FileSizeToFriendlyStringConverter _fileSizeConverter = new();
    private readonly IReadOnlyList<InspectorCategoryViewModel> _categories;
    private readonly IReadOnlyDictionary<string, InspectorFieldViewModel> _fields;

    public InspectorFieldValueUpdater(IReadOnlyList<InspectorCategoryViewModel> categories)
    {
        _categories = categories;
        _fields = categories
            .SelectMany(static category => category.Fields)
            .ToDictionary(static field => field.Key, StringComparer.OrdinalIgnoreCase);
    }

    public void ShowImmediateSelection(SpecFileEntryViewModel selectedItem)
    {
        ClearValues();

        if (selectedItem.Model is not { } model)
        {
            RefreshCategoryVisibility();
            return;
        }

        SetValue("Name", model.Name);
        SetValue("Full Path", model.FullPath.DisplayPath);
        SetValue("Type", model.Kind == ItemKind.Directory ? "Folder" : "File");
        SetValue("Extension", model.Extension);
        SetValue("Size", ConvertSize(model));
        SetValue("Attributes", model.Attributes.ToString());

        SetValue("Created", FormatLocalTime(model.CreationTime));
        SetValue("Modified", FormatLocalTime(model.LastWriteTime));
        SetAttributeFlags(model.Attributes);

        RefreshCategoryVisibility();
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

    private void RefreshCategoryVisibility()
    {
        foreach (var category in _categories)
        {
            category.RefreshVisibility();
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
