namespace WinUiFileManager.Interop.Types;

/// <summary>
/// Generic success/failure outcome for interop operations, carrying a native error code and optional message so
/// callers can report Win32 failures without exceptions. Construct via <see cref="Ok"/> / <see cref="Fail"/>.
/// </summary>
public sealed record InteropResult
{
    /// <summary>Prefer the <see cref="Ok"/> / <see cref="Fail"/> factories; this is exposed for record semantics.</summary>
    public InteropResult(bool success, int nativeErrorCode, string? errorMessage)
    {
        Success = success;
        NativeErrorCode = nativeErrorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The native (Win32) error code; <c>0</c> on success.</summary>
    public int NativeErrorCode { get; init; }

    /// <summary>Human-readable error description on failure; <see langword="null"/> on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Creates a successful result (code <c>0</c>, no message).</summary>
    public static InteropResult Ok() => new(true, 0, null);

    /// <summary>Creates a failed result with the given Win32 error code and message.</summary>
    public static InteropResult Fail(int errorCode, string message) => new(false, errorCode, message);
}
