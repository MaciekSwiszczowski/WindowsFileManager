namespace WinUiFileManager.Application.FileEntries;

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

    public NormalizedPath DirectoryPath { get; }

    public NormalizedPath FullPath => new(Path.Combine(DirectoryPath.Value, Name));

    public string Name { get; }

    public string Extension { get; }

    public ItemKind Kind { get; }

    public long? Size { get; }

    public DateTime LastWriteTime { get; }

    public DateTime CreationTime { get; }

    public FileAttributes Attributes { get; }
}
