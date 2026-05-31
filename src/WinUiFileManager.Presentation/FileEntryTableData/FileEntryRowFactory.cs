using System.IO.Enumeration;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.FileEntryTableData;

/// <summary>
/// Builds <see cref="SpecFileEntryViewModel"/> rows from the various filesystem sources used by the
/// scanner and reader: a native <see cref="FileSystemEntry"/> (bulk enumeration), a
/// <see cref="FileInfo"/>, or a <see cref="DirectoryInfo"/> (single-entry refresh). Centralises the
/// mapping from raw metadata to a <see cref="FileSystemEntryModel"/> so all three paths produce
/// identical rows.
/// </summary>
/// <remarks>
/// Extension strings are interned through <see cref="FileEntryDisplayStringCache"/> so the same handful
/// of extension values are shared across many rows instead of allocating per row (AGENTS.md §3). The
/// injected <see cref="Func{T,TResult}"/> is the actual row constructor (supplied by DI), keeping this
/// factory independent of how rows are instantiated.
/// </remarks>
internal sealed class FileEntryRowFactory
{
    private readonly Func<FileSystemEntryModel, SpecFileEntryViewModel> _rowFactory;
    private readonly FileEntryDisplayStringCache _displayStringCache;

    public FileEntryRowFactory(
        Func<FileSystemEntryModel, SpecFileEntryViewModel> rowFactory,
        FileEntryDisplayStringCache displayStringCache)
    {
        _rowFactory = rowFactory;
        _displayStringCache = displayStringCache;
    }

    /// <summary>Builds a row from a native enumeration record. Taken by <c>ref</c> because
    /// <see cref="FileSystemEntry"/> is a large ref struct yielded during enumeration; values are copied
    /// out into the model immediately. Directories report no size.</summary>
    public SpecFileEntryViewModel Create(NormalizedPath directoryPath, ref FileSystemEntry entry)
    {
        var isDirectory = entry.IsDirectory;
        var name = entry.FileName.ToString();
        var model = new FileSystemEntryModel(
            directoryPath,
            name,
            _displayStringCache.GetExtension(isDirectory ? string.Empty : Path.GetExtension(name)),
            isDirectory ? ItemKind.Directory : ItemKind.File,
            isDirectory ? null : entry.Length,
            entry.LastWriteTimeUtc.ToLocalTime().DateTime,
            entry.CreationTimeUtc.ToLocalTime().DateTime,
            entry.Attributes);

        return _rowFactory(model);
    }

    /// <summary>Builds a file row from a <see cref="FileInfo"/> (single-entry refresh path).</summary>
    public SpecFileEntryViewModel Create(NormalizedPath directoryPath, FileInfo fileInfo)
    {
        var model = new FileSystemEntryModel(
            directoryPath,
            fileInfo.Name,
            _displayStringCache.GetExtension(fileInfo.Extension),
            ItemKind.File,
            fileInfo.Length,
            fileInfo.LastWriteTime,
            fileInfo.CreationTime,
            fileInfo.Attributes);

        return _rowFactory(model);
    }

    /// <summary>Builds a directory row from a <see cref="DirectoryInfo"/> (single-entry refresh path);
    /// directories carry no size and an empty extension.</summary>
    public SpecFileEntryViewModel Create(NormalizedPath directoryPath, DirectoryInfo directoryInfo)
    {
        var model = new FileSystemEntryModel(
            directoryPath,
            directoryInfo.Name,
            _displayStringCache.GetExtension(string.Empty),
            ItemKind.Directory,
            null,
            directoryInfo.LastWriteTime,
            directoryInfo.CreationTime,
            directoryInfo.Attributes);

        return _rowFactory(model);
    }
}
