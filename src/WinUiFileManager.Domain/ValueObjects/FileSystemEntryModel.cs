using WinUiFileManager.Domain.Enums;

namespace WinUiFileManager.Domain.ValueObjects;

public sealed record FileSystemEntryModel
{
    public FileSystemEntryModel(
        DirectoryPath directoryPath,
        string name,
        string extension,
        ItemKind kind,
        long size,
        DateTime lastWriteTimeUtc,
        DateTime creationTimeUtc,
        FileAttributes attributes)
    {
        DirectoryPath = directoryPath;
        Name = name;
        Extension = extension;
        Kind = kind;
        Size = size;
        LastWriteTimeUtc = lastWriteTimeUtc;
        CreationTimeUtc = creationTimeUtc;
        Attributes = attributes;
    }

    public FileSystemEntryModel(
        NormalizedPath fullPath,
        string name,
        string extension,
        ItemKind kind,
        long size,
        DateTime lastWriteTimeUtc,
        DateTime creationTimeUtc,
        FileAttributes attributes)
        : this(
            DirectoryPath.FromEntryPath(fullPath),
            name,
            extension,
            kind,
            size,
            lastWriteTimeUtc,
            creationTimeUtc,
            attributes)
    {
    }

    public DirectoryPath DirectoryPath { get; }

    public NormalizedPath FullPath => DirectoryPath.GetEntryPath(Name);

    public string Name { get; }

    public string Extension { get; }

    public ItemKind Kind { get; }

    public long Size { get; }

    public DateTime LastWriteTimeUtc { get; }

    public DateTime CreationTimeUtc { get; }

    public FileAttributes Attributes { get; }
}
