using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Search;

/// <summary>
/// Drives the inspector's field search box. Filters the category fields in place by matching the search text
/// against each field's <see cref="InspectorFieldViewModelBase.SearchText"/>, then refreshes each category's
/// visibility so empty sections collapse. Operates on the shared category instances supplied via <see cref="Initialize"/>.
/// </summary>
public sealed partial class InspectorSearchViewModel : ObservableObject
{
    private IReadOnlyList<InspectorCategoryViewModel> _categories = [];

    /// <summary>Current search text; changing it triggers <see cref="Refresh(string)"/> via the generated partial hook.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Captures the categories to filter and performs an initial (unfiltered) refresh.</summary>
    public void Initialize(IReadOnlyList<InspectorCategoryViewModel> categories)
    {
        _categories = categories;
        Refresh();
    }

    /// <summary>
    /// Recomputes field visibility for the given search term (defaults to "show all"), then refreshes category
    /// visibility. An empty/whitespace term makes all fields visible.
    /// </summary>
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

    /// <summary>Re-filters whenever the bound search text changes.</summary>
    partial void OnSearchTextChanged(string value) => Refresh(value);

    /// <summary>A field is visible when there is no active search, or its search text contains the term (case-insensitive).</summary>
    private static bool ShouldFieldBeVisible(InspectorFieldViewModelBase field, string search, bool hasSearch) =>
        !hasSearch || field.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase);
}
