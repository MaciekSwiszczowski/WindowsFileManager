using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Domain.Errors;

public sealed record OperationError(
    NormalizedPath Path,
    FileOperationErrorCode Code,
    string Message,
    int? NativeErrorCode);
