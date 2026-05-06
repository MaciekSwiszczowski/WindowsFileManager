namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed record FileInspectorModel(
    ObservableCollection<FileInspectorFieldViewModel> Fields,
    ObservableCollection<FileInspectorCategoryViewModel> Categories,
    IReadOnlyDictionary<string, FileInspectorFieldViewModel> FieldMap,
    IReadOnlySet<string> DeferredFieldKeys);
