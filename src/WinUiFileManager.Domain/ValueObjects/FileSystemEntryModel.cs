using WinUiFileManager.Domain.Enums;

namespace WinUiFileManager.Domain.ValueObjects;

public sealed record FileSystemEntryModel(
    NormalizedPath FullPath,
    string Name,
    string Extension,
    ItemKind Kind,
    long Size,
    DateTime LastWriteTimeUtc,
    DateTime CreationTimeUtc,
    FileAttributes Attributes,
    NtfsFileId FileId);
