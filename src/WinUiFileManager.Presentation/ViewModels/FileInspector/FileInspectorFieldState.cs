namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class FileInspectorFieldState
{
    private readonly IReadOnlyDictionary<string, FileInspectorFieldViewModel> _fieldMap;
    private readonly IReadOnlySet<string> _deferredFieldKeys;

    public FileInspectorFieldState(FileInspectorModel model)
    {
        Fields = model.Fields;
        Categories = model.Categories;
        _fieldMap = model.FieldMap;
        _deferredFieldKeys = model.DeferredFieldKeys;
    }

    public ObservableCollection<FileInspectorFieldViewModel> Fields { get; }

    public ObservableCollection<FileInspectorCategoryViewModel> Categories { get; }

    public bool HasVisibleFields => Categories.Any(static category => category.HasVisibleFields);

    public void SetValue(string key, string value)
    {
        if (_fieldMap.TryGetValue(key, out var field))
        {
            field.Value = value;
        }
    }

    public void SetThumbnailSource(string key, ImageSource? value)
    {
        if (_fieldMap.TryGetValue(key, out var field))
        {
            field.ThumbnailSource = value;
        }
    }

    public void SetLoading(string key, bool isLoading)
    {
        if (_fieldMap.TryGetValue(key, out var field))
        {
            field.IsLoading = isLoading;
        }
    }

    public void ClearValues()
    {
        foreach (var field in Fields)
        {
            field.Value = string.Empty;
            field.ThumbnailSource = null;
            field.IsLoading = false;
            field.IsVisible = false;
        }
    }

    public void ClearDeferredFields()
    {
        foreach (var key in _deferredFieldKeys)
        {
            SetValue(key, string.Empty);
            SetLoading(key, false);
            if (string.Equals(key, "Thumbnail", StringComparison.OrdinalIgnoreCase))
            {
                SetThumbnailSource(key, null);
            }
        }
    }

    public void BeginDeferredRefresh()
    {
        foreach (var key in _deferredFieldKeys)
        {
            if (!_fieldMap.TryGetValue(key, out var field))
            {
                continue;
            }

            if (field.IsVisible)
            {
                field.IsLoading = true;
            }
        }
    }

    public void RefreshVisibleCategories(
        string currentFullPath,
        string searchText,
        bool preserveDeferredVisibility = false)
    {
        if (string.IsNullOrWhiteSpace(currentFullPath))
        {
            RefreshCategories();
            return;
        }

        var search = searchText.Trim();
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        foreach (var field in Fields)
        {
            if (preserveDeferredVisibility
                && IsDeferredField(field)
                && field.IsVisible)
            {
                continue;
            }

            field.IsVisible = ShouldFieldBeVisible(field, search, hasSearch);
        }

        RefreshCategories();
    }

    private void RefreshCategories()
    {
        foreach (var category in Categories.OrderBy(static category => FileInspectorCategorySort.GetSortOrder(category.Category)))
        {
            category.RefreshVisibility();
        }
    }

    private bool IsDeferredField(FileInspectorFieldViewModel field) =>
        _deferredFieldKeys.Contains(field.Key);

    private static bool ShouldFieldBeVisible(FileInspectorFieldViewModel field, string search, bool hasSearch)
    {
        if (field.IsLoading)
        {
            return true;
        }

        var hasValue = field.ThumbnailSource is not null || !string.IsNullOrWhiteSpace(field.Value);
        if (!hasValue)
        {
            return false;
        }

        return !hasSearch || field.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}
