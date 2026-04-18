using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Domain.Errors;

public sealed record OperationError
{
    public OperationError(
        NormalizedPath path,
        FileOperationErrorCode code,
        string message,
        int? nativeErrorCode)
    {
        Path = path;
        Code = code;
        Message = message;
        NativeErrorCode = nativeErrorCode;
    }

    public NormalizedPath Path { get; init; }

    public FileOperationErrorCode Code { get; init; }

    public string Message { get; init; }

    public int? NativeErrorCode { get; init; }
}
