namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FileInspectorFieldViewModel : ObservableObject
{
    private readonly string _searchPrefix;

    public FileInspectorFieldViewModel(FileInspectorCategory category, string key, string tooltip, string value = "", int sortOrder = 0)
    {
        Category = category;
        Key = key;
        Tooltip = tooltip;
        Value = value;
        SortOrder = sortOrder;
        _searchPrefix = string.Concat(Category.GetDisplayName(), " ", Key, " ");
    }

    public FileInspectorCategory Category { get; }

    public string Key { get; }

    public string Tooltip { get; }

    public int SortOrder { get; }

    [ObservableProperty]
    public partial string Value { get; set; }

    [ObservableProperty]
    public partial ImageSource? ThumbnailSource { get; set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool CanToggle { get; set; }

    [ObservableProperty]
    public partial bool IsToggleOn { get; set; }

    public IAsyncRelayCommand? ToggleCommand { get; private set; }

    public string DisplayValue => Value;

    public bool ShowsValue => !IsLoading && ThumbnailSource is null;

    public bool ShowsThumbnail => !IsLoading && ThumbnailSource is not null;

    public bool ShowsToggle => CanToggle && !IsLoading;

    public string SearchText => string.Concat(_searchPrefix, Value);

    public void ConfigureToggle(Func<bool, Task<bool>> toggleAsync)
    {
        CanToggle = true;
        ToggleCommand = new AsyncRelayCommand(() => ToggleAsync(toggleAsync));
        OnPropertyChanged(nameof(ShowsToggle));
    }

    partial void OnThumbnailSourceChanged(ImageSource? value)
    {
        OnPropertyChanged(nameof(ShowsValue));
        OnPropertyChanged(nameof(ShowsThumbnail));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowsValue));
        OnPropertyChanged(nameof(ShowsThumbnail));
        OnPropertyChanged(nameof(ShowsToggle));
    }

    partial void OnValueChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayValue));
        if (CanToggle)
        {
            IsToggleOn = string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
        }
    }

    partial void OnCanToggleChanged(bool value) => OnPropertyChanged(nameof(ShowsToggle));

    private async Task ToggleAsync(Func<bool, Task<bool>> toggleAsync)
    {
        var nextValue = !IsToggleOn;
        var previousValue = Value;
        var previousToggle = IsToggleOn;

        IsToggleOn = nextValue;
        Value = nextValue ? "Yes" : "No";

        if (await toggleAsync(nextValue))
        {
            return;
        }

        IsToggleOn = previousToggle;
        Value = previousValue;
    }
}
