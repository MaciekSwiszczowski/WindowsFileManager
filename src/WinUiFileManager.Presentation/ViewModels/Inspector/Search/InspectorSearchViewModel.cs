using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Search;

public sealed partial class InspectorSearchViewModel : ObservableObject
{
    private IReadOnlyList<InspectorCategoryViewModel> _categories = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public void Initialize(IReadOnlyList<InspectorCategoryViewModel> categories)
    {
        _categories = categories;
        Refresh();
    }

    public void Refresh(string value = "")
    {
        var search = value.Trim();
        var hasSearch = !string.IsNullOrWhiteSpace(search);

        foreach (var field in _categories.SelectMany(static category => category.Fields))
        {
            field.IsVisible = ShouldFieldBeVisible(field, search, hasSearch);
        }

        foreach (var category in _categories)
        {
            category.RefreshVisibility();
        }
    }

    partial void OnSearchTextChanged(string value) => Refresh(value);

    private static bool ShouldFieldBeVisible(InspectorFieldViewModel field, string search, bool hasSearch) =>
        !hasSearch || field.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase);
}
