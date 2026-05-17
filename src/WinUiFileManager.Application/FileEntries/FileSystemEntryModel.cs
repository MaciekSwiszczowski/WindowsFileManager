namespace WinUiFileManager.Application.FileEntries;

public sealed record FileSystemEntryModel
{
    public FileSystemEntryModel(
        DirectoryPath directoryPath,
        string name,
        string extension,
        ItemKind kind,
        long? size,
        DateTimeOffset lastWriteTime,
        DateTimeOffset creationTime,
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

    public DirectoryPath DirectoryPath { get; }

    public NormalizedPath FullPath => DirectoryPath.GetEntryPath(Name);

    public string Name { get; }

    public string Extension { get; }

    public ItemKind Kind { get; }

    public long? Size { get; }

    public DateTimeOffset LastWriteTime { get; }

    public DateTimeOffset CreationTime { get; }

    public FileAttributes Attributes { get; }
}
