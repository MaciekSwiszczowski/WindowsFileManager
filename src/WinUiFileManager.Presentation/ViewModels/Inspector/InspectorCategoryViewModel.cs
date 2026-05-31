using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

namespace WinUiFileManager.Presentation.ViewModels.Inspector;

/// <summary>
/// View model for one collapsible category section in the inspector (e.g. Basic, NTFS, Security). Holds the
/// section's fields and tracks its expand/collapse state and whether it currently has any visible fields
/// (used to hide empty sections during search filtering).
/// </summary>
public sealed partial class InspectorCategoryViewModel : ObservableObject
{
    public InspectorCategoryViewModel(FileInspectorCategory category)
    {
        Category = category;
        Name = category.GetDisplayName();
    }

    /// <summary>The category this section represents.</summary>
    public FileInspectorCategory Category { get; }

    /// <summary>The category's display header.</summary>
    public string Name { get; }

    /// <summary>Whether the section is expanded in the UI.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    /// <summary>Whether any field in this section is currently visible; recomputed by <see cref="RefreshVisibility"/>.</summary>
    [ObservableProperty]
    public partial bool HasVisibleFields { get; set; }

    /// <summary>The fields shown in this section.</summary>
    public ObservableCollection<InspectorFieldViewModelBase> Fields { get; } = [];

    /// <summary>Recomputes <see cref="HasVisibleFields"/> from the current field visibility (e.g. after a search filter).</summary>
    public void RefreshVisibility()
    {
        HasVisibleFields = Fields.Any(static field => field.IsVisible);
    }
}
