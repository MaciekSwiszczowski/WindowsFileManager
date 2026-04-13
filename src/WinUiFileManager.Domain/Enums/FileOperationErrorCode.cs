namespace WinUiFileManager.Domain.Enums;

public enum FileOperationErrorCode
{
    Unknown,
    AccessDenied,
    FileNotFound,
    DirectoryNotFound,
    PathTooLong,
    SharingViolation,
    FileLocked,
    DestinationExists,
    InvalidFileName,
    NotNtfsVolume,
    SourceMissing,
    OperationCancelled,
    IoError,
}
