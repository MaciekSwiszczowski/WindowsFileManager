using WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class FileInspectorModelBuilder
{
    private readonly Func<string, bool> _canToggleField;
    private readonly Func<string, bool, Task<bool>> _toggleFieldAsync;
    private readonly FileInspectorFieldViewModel.Factory _fieldFactory;
    private readonly FileInspectorCategoryViewModel.Factory _categoryFactory;

    public FileInspectorModelBuilder(
        Func<string, bool> canToggleField,
        Func<string, bool, Task<bool>> toggleFieldAsync,
        FileInspectorFieldViewModel.Factory fieldFactory,
        FileInspectorCategoryViewModel.Factory categoryFactory)
    {
        _canToggleField = canToggleField;
        _toggleFieldAsync = toggleFieldAsync;
        _fieldFactory = fieldFactory;
        _categoryFactory = categoryFactory;
    }

    public FileInspectorModel Build()
    {
        var fields = new ObservableCollection<FileInspectorFieldViewModel>();
        var categories = new ObservableCollection<FileInspectorCategoryViewModel>();
        var fieldMap = new Dictionary<string, FileInspectorFieldViewModel>(StringComparer.OrdinalIgnoreCase);
        var categoryMap = new Dictionary<FileInspectorCategory, FileInspectorCategoryViewModel>();
        var deferredFieldKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in CreateCategoryProviders())
        {
            foreach (var definition in provider.Fields)
            {
                var field = _fieldFactory(
                    definition.Category,
                    definition.Key,
                    definition.Tooltip,
                    string.Empty,
                    definition.SortOrder);

                if (_canToggleField(definition.Key))
                {
                    field.ConfigureToggle(enabled => _toggleFieldAsync(definition.Key, enabled));
                }

                fieldMap.Add(definition.Key, field);
                fields.Add(field);
                GetOrCreateCategory(definition.Category, categories, categoryMap).Fields.Add(field);

                if (definition.IsDeferred)
                {
                    deferredFieldKeys.Add(definition.Key);
                }
            }
        }

        foreach (var category in categories)
        {
            category.RefreshVisibility();
        }

        return new FileInspectorModel(fields, categories, fieldMap, deferredFieldKeys);
    }

    private static IReadOnlyList<IFileInspectorCategoryProvider> CreateCategoryProviders() =>
    [
        new BasicFileInspectorCategory(),
        new NtfsFileInspectorCategory(),
        new IdentityFileInspectorCategory(),
        new LocksFileInspectorCategory(),
        new LinksFileInspectorCategory(),
        new StreamsFileInspectorCategory(),
        new SecurityFileInspectorCategory(),
        new ThumbnailsFileInspectorCategory(),
        new CloudFileInspectorCategory()
    ];

    private FileInspectorCategoryViewModel GetOrCreateCategory(
        FileInspectorCategory category,
        ObservableCollection<FileInspectorCategoryViewModel> categories,
        Dictionary<FileInspectorCategory, FileInspectorCategoryViewModel> categoryMap)
    {
        if (categoryMap.TryGetValue(category, out var existingCategory))
        {
            return existingCategory;
        }

        var createdCategory = _categoryFactory(category);
        categoryMap.Add(category, createdCategory);
        var insertIndex = 0;
        while (insertIndex < categories.Count
               && FileInspectorCategorySort.GetSortOrder(categories[insertIndex].Category) <= FileInspectorCategorySort.GetSortOrder(category))
        {
            insertIndex++;
        }

        categories.Insert(insertIndex, createdCategory);
        return createdCategory;
    }
}
