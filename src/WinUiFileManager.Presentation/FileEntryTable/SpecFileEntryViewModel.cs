namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class SpecFileEntryViewModel
{
    private static readonly FilePathKey ParentEntryPathKey = new FilePathKey(ParentEntryKey);

    private const string ParentEntryKey = "\0..";

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

    // Used by FileEntryTableDataSource SourceCache. Real filesystem paths are normalized here so
    // initial scan rows and FileSystemWatcher updates use the same cache key shape.
    public FilePathKey GetKey()
    {
        return Model is null ? ParentEntryPathKey : new FilePathKey(Path.GetFullPath(Model.FullPath.DisplayPath));
    }
}
