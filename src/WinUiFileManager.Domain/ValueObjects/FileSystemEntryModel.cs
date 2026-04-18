using WinUiFileManager.Domain.Enums;

namespace WinUiFileManager.Domain.ValueObjects;

public sealed record FileSystemEntryModel
{
    public FileSystemEntryModel(
        NormalizedPath fullPath,
        string name,
        string extension,
        ItemKind kind,
        long size,
        DateTime lastWriteTimeUtc,
        DateTime creationTimeUtc,
        FileAttributes attributes,
        NtfsFileId fileId)
    {
        FullPath = fullPath;
        Name = name;
        Extension = extension;
        Kind = kind;
        Size = size;
        LastWriteTimeUtc = lastWriteTimeUtc;
        CreationTimeUtc = creationTimeUtc;
        Attributes = attributes;
        FileId = fileId;
    }

    public NormalizedPath FullPath { get; init; }

    public string Name { get; init; }

    public string Extension { get; init; }

    public ItemKind Kind { get; init; }

    public long Size { get; init; }

    public DateTime LastWriteTimeUtc { get; init; }

    public DateTime CreationTimeUtc { get; init; }

    public FileAttributes Attributes { get; init; }

    public NtfsFileId FileId { get; init; }
}
