using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

internal sealed class InspectorFieldValueUpdater
{
    private static readonly string[] SizeSuffixes = ["B", "KB", "MB", "GB", "TB"];

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
        SetValue("Size", FormatSize(model.Size));
        SetValue("Attributes", model.Attributes.ToString());

        SetValue("Created", FormatUtc(model.CreationTimeUtc));
        SetValue("Modified", FormatUtc(model.LastWriteTimeUtc));
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
        SetValue("System", FormatFlag(attributes.HasFlag(FileAttributes.System)));
        SetValue("Archive", FormatFlag(attributes.HasFlag(FileAttributes.Archive)));
        SetValue("Temporary", FormatFlag(attributes.HasFlag(FileAttributes.Temporary)));
        SetValue("Offline", FormatFlag(attributes.HasFlag(FileAttributes.Offline)));
        SetValue("Not Content Indexed", FormatFlag(attributes.HasFlag(FileAttributes.NotContentIndexed)));
        SetValue("Encrypted", FormatFlag(attributes.HasFlag(FileAttributes.Encrypted)));
        SetValue("Compressed", FormatFlag(attributes.HasFlag(FileAttributes.Compressed)));
        SetValue("Sparse", FormatFlag(attributes.HasFlag(FileAttributes.SparseFile)));
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

    private static string FormatUtc(DateTime value) =>
        value == DateTime.MinValue
            ? string.Empty
            : value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

    private static string FormatFlag(bool value) => value ? "Yes" : "No";

    private static string FormatSize(long bytes)
    {
        if (bytes < 0)
        {
            return string.Empty;
        }

        var suffixIndex = 0;
        var size = (double)bytes;

        while (size >= 1024 && suffixIndex < SizeSuffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return suffixIndex == 0
            ? $"{size:F0} {SizeSuffixes[suffixIndex]}"
            : $"{size:F2} {SizeSuffixes[suffixIndex]}";
    }
}
