namespace WinUiFileManager.Interop.Types;

public sealed record InteropResult(bool Success, int NativeErrorCode, string? ErrorMessage)
{
    public static InteropResult Ok() => new(true, 0, null);

    public static InteropResult Fail(int errorCode, string message) => new(false, errorCode, message);
}
