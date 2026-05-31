namespace WinUiFileManager.Presentation.FileEntryTable;

/// <summary>
/// The per-row view model bound to one <see cref="TableView"/> row in the file table. It is a thin,
/// immutable wrapper over a single <see cref="FileSystemEntryModel"/> (or the synthetic ".." parent
/// row, represented by a null <see cref="Model"/>).
/// </summary>
/// <remarks>
/// <b>This type must stay lean (AGENTS.md §2/§3).</b> A folder can produce tens of thousands of these,
/// so it deliberately has no <see cref="System.ComponentModel.INotifyPropertyChanged"/>, no derived/
/// cached display strings, and no per-row UI state — every extra field is multiplied across all rows.
/// Display formatting is computed on demand in cell templates/converters via
/// <see cref="WinUiFileManager.Presentation.Services.FileEntryDisplayStringCache"/>, and the parent
/// row is identified by a null model rather than by an extra flag.
/// </remarks>
public sealed class SpecFileEntryViewModel
{
    private static readonly FilePathKey ParentEntryPathKey = new FilePathKey(ParentEntryKey);

    // Sentinel key for the synthetic ".." row. The leading NUL guarantees it can never collide with a
    // real filesystem path (which cannot contain NUL).
    private const string ParentEntryKey = "\0..";

    // Keep this row model lean. Do not add presentation state or convenience properties here:
    // large folders can create tens of thousands of instances, so extra fields directly increase memory use.
    /// <summary>Creates a row for a real filesystem entry.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
    public SpecFileEntryViewModel(FileSystemEntryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
    }

    // Private no-model ctor used only for the synthetic parent ("..") row.
    private SpecFileEntryViewModel() => Model = null;

    /// <summary>Creates the synthetic ".." navigation row (its <see cref="Model"/> is null).</summary>
    public static SpecFileEntryViewModel CreateParentEntry() => new();

    /// <summary>True when <paramref name="item"/> is the synthetic ".." row rather than a real entry.</summary>
    public static bool IsParentEntry(SpecFileEntryViewModel item) => item.Model is null;

    /// <summary>The wrapped filesystem entry, or null for the synthetic parent row.</summary>
    public FileSystemEntryModel? Model { get; }

    // Used by FileEntryTableDataSource SourceCache. Real filesystem paths are normalized here so
    // initial scan rows and FileSystemWatcher updates use the same cache key shape.
    /// <summary>Returns the <see cref="FilePathKey"/> identity used to key this row in the data
    /// source's <c>SourceCache</c>. The parent row maps to a fixed sentinel key; real rows normalise
    /// the full path so a scan result and a later watcher update for the same file collide on one key.</summary>
    public FilePathKey GetKey()
    {
        return Model is null ? ParentEntryPathKey : new FilePathKey(Path.GetFullPath(Model.FullPath.DisplayPath));
    }
}
