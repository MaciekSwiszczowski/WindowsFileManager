namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class PanelViewModel : ObservableObject
{
    public PanelViewModel(string identity)
    {
        Identity = identity;
    }

    public string Identity { get; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial string CurrentPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ItemCount { get; set; }

    [ObservableProperty]
    public partial int SelectedCount { get; set; }
}
