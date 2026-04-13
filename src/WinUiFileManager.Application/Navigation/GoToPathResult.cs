using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Navigation;

public sealed record GoToPathResult(
    bool Success,
    NormalizedPath? Path,
    IReadOnlyList<FileSystemEntryModel>? Entries,
    string? ErrorMessage);
