using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
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

    [ObservableProperty]
    public partial double ContentWidth { get; set; }

    [ObservableProperty]
    public partial bool HasVisibleFields { get; set; }

    public ObservableCollection<FileInspectorFieldViewModel> Fields { get; } = [];

    public Visibility Visibility => HasVisibleFields ? Visibility.Visible : Visibility.Collapsed;

    public void RefreshVisibility()
    {
        HasVisibleFields = Fields.Any(static field => field.IsVisible);
    }

    partial void OnHasVisibleFieldsChanged(bool value) => OnPropertyChanged(nameof(Visibility));
}
