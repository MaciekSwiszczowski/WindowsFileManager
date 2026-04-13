using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Domain.Errors;

public sealed record OperationWarning(
    NormalizedPath Path,
    string Message);
