using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

namespace WinUiFileManager.Presentation.ViewModels.Inspector;

public sealed partial class InspectorCategoryViewModel : ObservableObject
{
    public InspectorCategoryViewModel(FileInspectorCategory category)
    {
        Category = category;
        Name = category.GetDisplayName();
    }

    public FileInspectorCategory Category { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    [ObservableProperty]
    public partial bool HasVisibleFields { get; set; }

    public ObservableCollection<InspectorFieldViewModel> Fields { get; } = [];

    public void RefreshVisibility()
    {
        HasVisibleFields = Fields.Any(static field => field.IsVisible);
    }
}
