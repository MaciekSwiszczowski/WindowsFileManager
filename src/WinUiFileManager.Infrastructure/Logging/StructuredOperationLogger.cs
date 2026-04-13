using Microsoft.Extensions.Logging;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Infrastructure.Logging;

public sealed class StructuredOperationLogger
{
    private readonly ILogger<StructuredOperationLogger> _logger;

    public StructuredOperationLogger(ILogger<StructuredOperationLogger> logger)
    {
        _logger = logger;
    }

    public void LogOperationStarted(
        OperationType type, int itemCount, NormalizedPath? destination)
    {
        _logger.LogInformation(
            "Operation {OperationType} started: {ItemCount} items, destination {Destination}",
            type, itemCount, destination?.DisplayPath ?? "(none)");
    }

    public void LogOperationCompleted(
        OperationType type, OperationStatus status, TimeSpan duration, int succeeded, int failed)
    {
        _logger.LogInformation(
            "Operation {OperationType} completed: {Status} in {DurationMs}ms — {Succeeded} succeeded, {Failed} failed",
            type, status, duration.TotalMilliseconds, succeeded, failed);
    }

    public void LogOperationCancelled(
        OperationType type, int completedItems, int totalItems)
    {
        _logger.LogWarning(
            "Operation {OperationType} cancelled after {CompletedItems}/{TotalItems} items",
            type, completedItems, totalItems);
    }

    public void LogItemFailure(
        NormalizedPath path,
        FileOperationErrorCode errorCode,
        string message,
        int? nativeErrorCode)
    {
        _logger.LogError(
            "Item failed: {Path} — {ErrorCode}: {Message} (native error {NativeErrorCode})",
            path.DisplayPath, errorCode, message, nativeErrorCode);
    }

    public void LogCollisionResolution(NormalizedPath path, CollisionPolicy resolution)
    {
        _logger.LogInformation(
            "Collision resolved for {Path} with policy {Resolution}",
            path.DisplayPath, resolution);
    }
}
