namespace WinUiFileManager.Interop.Types;

public sealed record FileIdResult(bool Success, byte[]? FileId128, string? ErrorMessage);
