namespace WinUiFileManager.Application.FileEntries;

public sealed record DirectoryChange(
    DirectoryChangeKind Kind,
    NormalizedPath Path,
    NormalizedPath? OldPath = null);
