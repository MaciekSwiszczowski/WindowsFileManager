using DynamicData.Binding;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTable.Data;

internal sealed record FileEntryTableDataState(
    string CurrentPath,
    ObservableCollectionExtended<SpecFileEntryViewModel> Items);
