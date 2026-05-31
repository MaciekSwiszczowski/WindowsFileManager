namespace WinUiFileManager.Application.FileEntries;

/// <summary>Classifies a <see cref="DirectoryChange"/> notification.</summary>
public enum DirectoryChangeKind
{
    /// <summary>A new entry appeared in the watched directory.</summary>
    Created,

    /// <summary>An entry was removed from the watched directory.</summary>
    Deleted,

    /// <summary>An existing entry's contents or metadata changed.</summary>
    Changed,

    /// <summary>An entry was renamed; both old and new paths are reported on the change.</summary>
    Renamed,
}
