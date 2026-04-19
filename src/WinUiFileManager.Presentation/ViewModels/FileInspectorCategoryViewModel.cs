using System.Collections.ObjectModel;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed class FileInspectorCategoryViewModel
{
    public FileInspectorCategoryViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public ObservableCollection<FileInspectorFieldViewModel> Fields { get; } = [];
}
