using WinUiFileManager.Domain.Errors;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Domain.Results;

public sealed record OperationItemResult(
    NormalizedPath SourcePath,
    NormalizedPath? DestinationPath,
    bool Succeeded,
    OperationError? Error,
    OperationWarning? Warning);
