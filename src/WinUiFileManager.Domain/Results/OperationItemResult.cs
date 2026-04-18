using WinUiFileManager.Domain.Errors;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Domain.Results;

public sealed record OperationItemResult
{
    public OperationItemResult(
        NormalizedPath sourcePath,
        NormalizedPath? destinationPath,
        bool succeeded,
        OperationError? error,
        OperationWarning? warning)
    {
        SourcePath = sourcePath;
        DestinationPath = destinationPath;
        Succeeded = succeeded;
        Error = error;
        Warning = warning;
    }

    public NormalizedPath SourcePath { get; init; }

    public NormalizedPath? DestinationPath { get; init; }

    public bool Succeeded { get; init; }

    public OperationError? Error { get; init; }

    public OperationWarning? Warning { get; init; }
}
