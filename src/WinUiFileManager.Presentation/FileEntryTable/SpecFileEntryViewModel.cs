namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class SpecFileEntryViewModel
{
    // Keep this row model lean. Do not add presentation state or convenience properties here:
    // large folders can create tens of thousands of instances, so extra fields directly increase memory use.
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
