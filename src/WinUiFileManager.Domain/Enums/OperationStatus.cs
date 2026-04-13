namespace WinUiFileManager.Domain.Enums;

public enum OperationStatus
{
    Succeeded,
    CompletedWithWarnings,
    CompletedWithErrors,
    Cancelled,
    Failed,
    FailedValidation,
}
