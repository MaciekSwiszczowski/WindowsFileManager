using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;

using CommunityToolkit.Mvvm.ComponentModel;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FileInspectorCategoryViewModel : ObservableObject
{
    public FileInspectorCategoryViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    public ObservableCollection<FileInspectorFieldViewModel> Fields { get; } = [];

    public ObservableCollection<FileInspectorFieldViewModel> VisibleFields { get; } = [];

    public Visibility Visibility => VisibleFields.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    public void RefreshVisibleFields()
    {
        VisibleFields.Clear();

        foreach (var field in Fields.Where(static field => field.IsVisible))
        {
            VisibleFields.Add(field);
        }

        OnPropertyChanged(nameof(Visibility));
    }
}
