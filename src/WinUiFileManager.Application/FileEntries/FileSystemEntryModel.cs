namespace WinUiFileManager.Application.FileEntries;

/// <summary>
/// Immutable domain model for a single file-system entry (file or directory) shown in a pane.
/// </summary>
/// <remarks>
/// This is the source of truth that the lean row view model (<c>FileListingRow</c>) wraps;
/// the table targets ~10k+ rows, so this type intentionally stores only raw NTFS facts and derives
/// nothing eagerly (see AGENTS.md §3). Display formatting happens on demand in converters/cell templates.
/// </remarks>
public sealed record FileSystemEntryModel
{
    public FileSystemEntryModel(
        NormalizedPath directoryPath,
        string name,
        string extension,
        ItemKind kind,
        long? size,
        DateTime lastWriteTime,
        DateTime creationTime,
        FileAttributes attributes)
    {
        DirectoryPath = directoryPath;
        Name = name;
        Extension = extension;
        Kind = kind;
        Size = size;
        LastWriteTime = lastWriteTime;
        CreationTime = creationTime;
        Attributes = attributes;
    }

    /// <summary>The normalized path of the directory that contains this entry.</summary>
    public NormalizedPath DirectoryPath { get; }

    /// <summary>
    /// The full normalized path (<see cref="DirectoryPath"/> + <see cref="Name"/>).
    /// </summary>
    /// <remarks>
    /// Hot path: this allocates a new <see cref="NormalizedPath"/> (and a combined string) on every
    /// access and is read by every row binding/key/log line (see AGENTS.md §3). Memoize at the call
    /// site instead of recomputing per row.
    /// </remarks>
    public NormalizedPath FullPath => new(Path.Combine(DirectoryPath.Value, Name));

    /// <summary>File or directory name including extension (no directory component).</summary>
    public string Name { get; }

    /// <summary>File extension (including the leading dot, per the producing service); empty for directories.</summary>
    public string Extension { get; }

    /// <summary>Whether this entry is a file or a directory.</summary>
    public ItemKind Kind { get; }

    /// <summary>Size in bytes, or <see langword="null"/> when not applicable (e.g. directories) or unavailable.</summary>
    public long? Size { get; }

    /// <summary>Last-write timestamp as reported by the file system.</summary>
    public DateTime LastWriteTime { get; }

    /// <summary>Creation timestamp as reported by the file system.</summary>
    public DateTime CreationTime { get; }

    /// <summary>Raw NTFS file attribute flags (hidden, system, read-only, reparse point, etc.).</summary>
    public FileAttributes Attributes { get; }
}
