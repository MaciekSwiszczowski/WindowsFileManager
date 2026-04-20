using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FileInspectorFieldViewModel : ObservableObject
{
    private readonly string _searchPrefix;

    public FileInspectorFieldViewModel(string category, string key, string tooltip, string value = "", int sortOrder = 0)
    {
        Category = category;
        Key = key;
        Tooltip = tooltip;
        Value = value;
        SortOrder = sortOrder;
        _searchPrefix = string.Concat(Category, " ", Key, " ");
    }

    public string Category { get; }

    public string Key { get; }

    public string Tooltip { get; }

    public int SortOrder { get; }

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ImageSource? ThumbnailSource { get; set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public string DisplayValue => Value;

    public Visibility ValueVisibility => !IsLoading && ThumbnailSource is null
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility ThumbnailVisibility => !IsLoading && ThumbnailSource is not null
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RowVisibility => IsVisible ? Visibility.Visible : Visibility.Collapsed;

    public string SearchText => string.Concat(_searchPrefix, Value);

    partial void OnThumbnailSourceChanged(ImageSource? value)
    {
        OnPropertyChanged(nameof(ValueVisibility));
        OnPropertyChanged(nameof(ThumbnailVisibility));
    }

    partial void OnIsVisibleChanged(bool value) => OnPropertyChanged(nameof(RowVisibility));

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ValueVisibility));
        OnPropertyChanged(nameof(ThumbnailVisibility));
        OnPropertyChanged(nameof(LoadingVisibility));
    }

    partial void OnValueChanged(string value) => OnPropertyChanged(nameof(DisplayValue));
}
