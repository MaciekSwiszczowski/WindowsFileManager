using System.Collections.Concurrent;
using System.IO.Enumeration;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

internal sealed class FileEntryRowFactory
{
    private static readonly ConcurrentDictionary<string, string> ExtensionPool = new(StringComparer.OrdinalIgnoreCase);

    public SpecFileEntryViewModel Create(NormalizedPath directoryPath, ref FileSystemEntry entry)
    {
        var isDirectory = entry.IsDirectory;
        var name = entry.FileName.ToString();
        var model = new FileSystemEntryModel(
            directoryPath,
            name,
            InternExtension(isDirectory ? string.Empty : Path.GetExtension(name)),
            isDirectory ? ItemKind.Directory : ItemKind.File,
            isDirectory ? null : entry.Length,
            entry.LastWriteTimeUtc.ToLocalTime().DateTime,
            entry.CreationTimeUtc.ToLocalTime().DateTime,
            entry.Attributes);

        return new SpecFileEntryViewModel(model);
    }

    public SpecFileEntryViewModel Create(NormalizedPath directoryPath, FileInfo fileInfo)
    {
        var model = new FileSystemEntryModel(
            directoryPath,
            fileInfo.Name,
            InternExtension(fileInfo.Extension),
            ItemKind.File,
            fileInfo.Length,
            fileInfo.LastWriteTime,
            fileInfo.CreationTime,
            fileInfo.Attributes);

        return new SpecFileEntryViewModel(model);
    }

    public SpecFileEntryViewModel Create(NormalizedPath directoryPath, DirectoryInfo directoryInfo)
    {
        var model = new FileSystemEntryModel(
            directoryPath,
            directoryInfo.Name,
            InternExtension(string.Empty),
            ItemKind.Directory,
            null,
            directoryInfo.LastWriteTime,
            directoryInfo.CreationTime,
            directoryInfo.Attributes);

        return new SpecFileEntryViewModel(model);
    }

    private static string InternExtension(string extension) =>
        string.IsNullOrEmpty(extension)
            ? string.Empty
            : ExtensionPool.GetOrAdd(extension, static value => value);
}
