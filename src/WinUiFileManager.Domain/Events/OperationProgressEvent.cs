using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Domain.Events;

public sealed record OperationProgressEvent
{
    public OperationProgressEvent(
        OperationType type,
        int totalItems,
        int completedItems,
        long totalBytes,
        long completedBytes,
        NormalizedPath? currentItemPath,
        string? statusMessage)
    {
        Type = type;
        TotalItems = totalItems;
        CompletedItems = completedItems;
        TotalBytes = totalBytes;
        CompletedBytes = completedBytes;
        CurrentItemPath = currentItemPath;
        StatusMessage = statusMessage;
    }

    public OperationType Type { get; init; }

    public int TotalItems { get; init; }

    public int CompletedItems { get; init; }

    public long TotalBytes { get; init; }

    public long CompletedBytes { get; init; }

    public NormalizedPath? CurrentItemPath { get; init; }

    public string? StatusMessage { get; init; }
}
