using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Domain.Operations;

public sealed record OperationItemPlan(
    NormalizedPath SourcePath,
    NormalizedPath? DestinationPath,
    ItemKind Kind,
    long EstimatedSize);
