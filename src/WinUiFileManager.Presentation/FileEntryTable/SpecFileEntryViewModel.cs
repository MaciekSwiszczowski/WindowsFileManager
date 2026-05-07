namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class SpecFileEntryViewModel
{
    public SpecFileEntryViewModel(FileSystemEntryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        Model = model;
    }

    private SpecFileEntryViewModel() => Model = null;

    public static SpecFileEntryViewModel CreateParentEntry() => new();

    public static bool IsParentEntry(SpecFileEntryViewModel item) => item.Model is null;

    public FileSystemEntryModel? Model { get; }
}
