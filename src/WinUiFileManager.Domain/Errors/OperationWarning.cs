using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Domain.Errors;

public sealed record OperationWarning
{
    public OperationWarning(NormalizedPath path, string message)
    {
        Path = path;
        Message = message;
    }

    public NormalizedPath Path { get; init; }

    public string Message { get; init; }
}
