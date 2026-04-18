namespace WinUiFileManager.Interop.Types;

public sealed record FileIdResult
{
    public FileIdResult(bool success, byte[]? fileId128, string? errorMessage)
    {
        Success = success;
        FileId128 = fileId128;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; init; }

    public byte[]? FileId128 { get; init; }

    public string? ErrorMessage { get; init; }
}
