using DynamicData.Binding;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.TestApp;

internal sealed record FileEntryTableDataState(
    string CurrentPath,
    ObservableCollectionExtended<SpecFileEntryViewModel> Items);
