namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class StatusBarViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string ActivePaneName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ItemCount { get; set; }

    [ObservableProperty]
    public partial int SelectedCount { get; set; }

    [ObservableProperty]
    public partial string SelectedSize { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }
}
