namespace WinUiFileManager.Application.FileEntries;

public sealed record DirectoryChange(DirectoryChangeKind Kind, string Path, string? OldPath = null);
