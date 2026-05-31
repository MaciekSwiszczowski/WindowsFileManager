using System.IO.Enumeration;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.FileEntryTableData;

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
