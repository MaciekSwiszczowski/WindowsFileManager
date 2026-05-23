using System.Collections.Concurrent;
using System.IO.Enumeration;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

internal sealed class WindowsFileEntryDataReader : IFileEntryDataReader
{
    private static readonly ConcurrentDictionary<string, string> ExtensionPool = new(StringComparer.OrdinalIgnoreCase);

    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
    };

    public IReadOnlyList<SpecFileEntryViewModel> GetEntries(NormalizedPath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var displayPath = path.DisplayPath;
        if (!Directory.Exists(displayPath))
        {
            return [];
        }

        var entries = new List<SpecFileEntryViewModel>();
        if (Directory.GetParent(displayPath) is not null)
        {
            entries.Add(SpecFileEntryViewModel.CreateParentEntry());
        }

        foreach (var entry in CreateDirectoryEnumerable(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(new SpecFileEntryViewModel(entry));
        }

        return entries;
    }

    public SpecFileEntryViewModel? GetEntry(NormalizedPath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var displayPath = path.DisplayPath;
        FileSystemEntryModel? model = null;

        if (File.Exists(displayPath))
        {
            model = BuildEntryModel(new FileInfo(displayPath));
        }
        else if (Directory.Exists(displayPath))
        {
            model = BuildEntryModel(new DirectoryInfo(displayPath));
        }

        return model is null ? null : new SpecFileEntryViewModel(model);
    }

    private static FileSystemEntryModel BuildEntryModel(FileInfo fileInfo)
    {
        var parentPath = Path.GetDirectoryName(fileInfo.FullName);

        return new FileSystemEntryModel(
            NormalizedPath.FromFullyQualifiedPath(string.IsNullOrWhiteSpace(parentPath) ? fileInfo.FullName : parentPath),
            fileInfo.Name,
            InternExtension(fileInfo.Extension),
            ItemKind.File,
            fileInfo.Length,
            fileInfo.LastWriteTime,
            fileInfo.CreationTime,
            fileInfo.Attributes);
    }

    private static FileSystemEntryModel BuildEntryModel(DirectoryInfo directoryInfo)
    {
        var parentPath = Path.GetDirectoryName(directoryInfo.FullName);

        return new FileSystemEntryModel(
            NormalizedPath.FromFullyQualifiedPath(string.IsNullOrWhiteSpace(parentPath) ? directoryInfo.FullName : parentPath),
            directoryInfo.Name,
            InternExtension(string.Empty),
            ItemKind.Directory,
            null,
            directoryInfo.LastWriteTime,
            directoryInfo.CreationTime,
            directoryInfo.Attributes);
    }

    private static FileSystemEntryModel BuildEntryModel(NormalizedPath directoryPath, ref FileSystemEntry entry)
    {
        var isDirectory = entry.IsDirectory;
        var kind = isDirectory ? ItemKind.Directory : ItemKind.File;
        var name = entry.FileName.ToString();
        var extension = isDirectory ? string.Empty : Path.GetExtension(name);

        return new FileSystemEntryModel(
            directoryPath,
            name,
            InternExtension(extension),
            kind,
            isDirectory ? null : entry.Length,
            entry.LastWriteTimeUtc.ToLocalTime().DateTime,
            entry.CreationTimeUtc.ToLocalTime().DateTime,
            entry.Attributes);
    }

    private static FileSystemEnumerable<FileSystemEntryModel> CreateDirectoryEnumerable(NormalizedPath directoryPath)
    {
        return new FileSystemEnumerable<FileSystemEntryModel>(
            directoryPath.DisplayPath,
            (ref entry) => BuildEntryModel(directoryPath, ref entry),
            EnumerationOptions);
    }

    private static string InternExtension(string extension) =>
        string.IsNullOrEmpty(extension)
            ? string.Empty
            : ExtensionPool.GetOrAdd(extension, static value => value);
}
