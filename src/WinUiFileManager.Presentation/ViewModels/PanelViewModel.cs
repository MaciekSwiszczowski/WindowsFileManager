using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class PanelViewModel : ObservableObject
{
    public PanelViewModel(string identity, IFileSystemService fileSystemService, IMessenger messenger)
    {
        Identity = identity;
        FileSystemService = fileSystemService;
        Messenger = messenger;
    }

    public string Identity { get; }

    public IFileSystemService FileSystemService { get; }

    public IMessenger Messenger { get; }

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

    [ObservableProperty]
    public partial ObservableCollection<SpecFileEntryViewModel>? Items { get; set; }

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

    partial void OnItemsChanged(ObservableCollection<SpecFileEntryViewModel>? value) =>
        ItemCount = value?.Count ?? 0;
}
