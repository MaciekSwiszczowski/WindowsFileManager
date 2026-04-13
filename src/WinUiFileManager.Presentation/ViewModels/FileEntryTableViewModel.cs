using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FileEntryTableViewModel : ObservableObject
{
    private FilePaneViewModel? _host;

    public FilePaneViewModel? Host => _host;

    public object? Items => _host?.Items;

    public event EventHandler? SortStateChanged;

    public void Attach(FilePaneViewModel? host)
    {
        if (ReferenceEquals(_host, host))
            return;

        if (_host is not null)
            _host.PropertyChanged -= OnHostPropertyChanged;

        _host = host;

        if (_host is not null)
            _host.PropertyChanged += OnHostPropertyChanged;

        OnPropertyChanged(nameof(Items));
        RaiseSortStateChanged();
    }

    private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FilePaneViewModel.SortBy)
            or nameof(FilePaneViewModel.SortAscending))
            RaiseSortStateChanged();
    }

    private void RaiseSortStateChanged() =>
        SortStateChanged?.Invoke(this, EventArgs.Empty);

    public void ApplySortFromSortMemberPath(string? sortMemberPath)
    {
        if (_host is null || string.IsNullOrEmpty(sortMemberPath))
            return;

        _host.SetSort(MapSortMemberPath(sortMemberPath));
    }

    public static SortColumn MapSortMemberPath(string path) => path switch
    {
        nameof(FileEntryViewModel.Name) => SortColumn.Name,
        nameof(FileEntryViewModel.Extension) => SortColumn.Extension,
        nameof(FileEntryViewModel.Size) => SortColumn.Size,
        nameof(FileEntryViewModel.LastWriteTime) => SortColumn.Modified,
        nameof(FileEntryViewModel.Attributes) => SortColumn.Attributes,
        nameof(FileEntryViewModel.FileId) => SortColumn.FileId,
        _ => SortColumn.Name,
    };
}
