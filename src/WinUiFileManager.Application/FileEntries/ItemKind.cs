namespace WinUiFileManager.Application.FileEntries;

/// <summary>Distinguishes the two kinds of <see cref="FileSystemEntryModel"/> entries.</summary>
public enum ItemKind
{
    /// <summary>A regular file.</summary>
    File,

    /// <summary>A directory (folder).</summary>
    Directory,
}
