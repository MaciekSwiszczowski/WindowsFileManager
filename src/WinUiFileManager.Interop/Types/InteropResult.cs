namespace WinUiFileManager.Interop.Types;

public sealed record InteropResult
{
    public InteropResult(bool success, int nativeErrorCode, string? errorMessage)
    {
        Success = success;
        NativeErrorCode = nativeErrorCode;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; init; }

    public int NativeErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static InteropResult Ok() => new(true, 0, null);

    public static InteropResult Fail(int errorCode, string message) => new(false, errorCode, message);
}
