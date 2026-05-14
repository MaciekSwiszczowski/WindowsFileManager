namespace WinUiFileManager.Presentation.ViewModels.Inspector;

public sealed partial class InspectorViewModel : ObservableObject
{
    public InspectorCommandsViewModel Commands { get; } = new();

    [ObservableProperty]
    public partial FileInspectorSelectionMode SelectionMode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MultiSelectionStatusText))]
    public partial int SelectedItemCount { get; set; }

    public string MultiSelectionStatusText => SelectedItemCount == 1
        ? "1 item selected"
        : $"{SelectedItemCount} items selected";

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public ObservableCollection<FileInspectorCategoryViewModel> Categories { get; } = [];
}
