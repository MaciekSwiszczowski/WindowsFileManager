using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Domain.Events;

public sealed record OperationProgressEvent(
    OperationType Type,
    int TotalItems,
    int CompletedItems,
    long TotalBytes,
    long CompletedBytes,
    NormalizedPath? CurrentItemPath,
    string? StatusMessage);
