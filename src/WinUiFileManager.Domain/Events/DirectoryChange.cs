using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Domain.Events;

public sealed record DirectoryChange(
    DirectoryChangeKind Kind,
    NormalizedPath Path,
    NormalizedPath? OldPath = null);
