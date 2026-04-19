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

    public Visibility ValueVisibility => ThumbnailSource is null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ThumbnailVisibility => ThumbnailSource is null ? Visibility.Collapsed : Visibility.Visible;

    public string SearchText => string.Concat(_searchPrefix, Value);

    partial void OnThumbnailSourceChanged(ImageSource? value)
    {
        OnPropertyChanged(nameof(ValueVisibility));
        OnPropertyChanged(nameof(ThumbnailVisibility));
    }
}
