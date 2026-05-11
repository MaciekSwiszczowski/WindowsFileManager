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
    public partial string EditablePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PathValidationMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ItemCount { get; set; }

    [ObservableProperty]
    public partial int SelectedCount { get; set; }

    public string SummaryText => $"{ItemCount} items, {SelectedCount} selected";

    public bool HasPathValidationError => !string.IsNullOrWhiteSpace(PathValidationMessage);

    partial void OnCurrentPathChanged(string value)
    {
        EditablePath = value;
        PathValidationMessage = string.Empty;
        OnPropertyChanged(nameof(HasPathValidationError));
    }

    partial void OnPathValidationMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasPathValidationError));

    partial void OnItemCountChanged(int value) =>
        OnPropertyChanged(nameof(SummaryText));

    partial void OnSelectedCountChanged(int value) =>
        OnPropertyChanged(nameof(SummaryText));
}
