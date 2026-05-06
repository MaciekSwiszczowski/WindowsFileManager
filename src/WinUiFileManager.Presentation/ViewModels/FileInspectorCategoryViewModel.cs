namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FileInspectorCategoryViewModel : ObservableObject
{
    public FileInspectorCategoryViewModel(FileInspectorCategory category)
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

    public ObservableCollection<FileInspectorFieldViewModel> Fields { get; } = [];

    public void RefreshVisibility()
    {
        HasVisibleFields = Fields.Any(static field => field.IsVisible);
    }
}
