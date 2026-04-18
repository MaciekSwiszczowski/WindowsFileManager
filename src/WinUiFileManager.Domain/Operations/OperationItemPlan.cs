using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Domain.Operations;

public sealed record OperationItemPlan
{
    public OperationItemPlan(
        NormalizedPath sourcePath,
        NormalizedPath? destinationPath,
        ItemKind kind,
        long estimatedSize)
    {
        SourcePath = sourcePath;
        DestinationPath = destinationPath;
        Kind = kind;
        EstimatedSize = estimatedSize;
    }

    public NormalizedPath SourcePath { get; init; }

    public NormalizedPath? DestinationPath { get; init; }

    public ItemKind Kind { get; init; }

    public long EstimatedSize { get; init; }
}
