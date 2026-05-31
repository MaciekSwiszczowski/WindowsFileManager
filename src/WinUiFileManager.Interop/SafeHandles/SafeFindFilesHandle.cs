using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

namespace WinUiFileManager.Interop.SafeHandles;

/// <summary>
/// <see cref="SafeHandle"/> wrapper around the search handle returned by <c>FindFirstStreamW</c>, used to
/// enumerate a file's NTFS alternate data streams. Owns the handle and guarantees it is released through
/// <c>FindClose</c> exactly once, satisfying the project rule that native search handles use a
/// <see cref="SafeHandle"/> rather than a raw <see cref="nint"/> with manual <c>try/finally</c>.
/// </summary>
/// <remarks>
/// The base class is <see cref="SafeHandleZeroOrMinusOneIsInvalid"/> because <c>FindFirstStreamW</c> reports
/// failure as <c>INVALID_HANDLE_VALUE</c> (-1); a wrapper constructed from a failed call therefore reports
/// <see cref="SafeHandle.IsInvalid"/> = <see langword="true"/>. Not thread-safe: enumeration via
/// <see cref="TryReadNextStream"/> is stateful and intended for single-threaded use within one enumeration loop.
/// </remarks>
internal sealed class SafeFindFilesHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Takes ownership of a search handle produced by <c>FindFirstStreamW</c>.</summary>
    /// <param name="preexistingHandle">The raw search handle (may be <c>INVALID_HANDLE_VALUE</c> on failure).</param>
    internal unsafe SafeFindFilesHandle(HANDLE preexistingHandle)
        : base(ownsHandle: true)
        => SetHandle((nint)preexistingHandle.Value);

    private HANDLE DangerousGetHandleForPInvoke() => new(DangerousGetHandle());

    /// <summary>Advances the enumeration to the next stream via <c>FindNextStreamW</c>.</summary>
    /// <param name="data">Receives the next stream's data when the call succeeds.</param>
    /// <returns><see langword="true"/> if another stream was read; <see langword="false"/> at end of enumeration.</returns>
    internal unsafe bool TryReadNextStream(ref WIN32_FIND_STREAM_DATA data)
    {
        // Pin the caller's struct so the native call can write directly into it for the duration of the P/Invoke.
        fixed (WIN32_FIND_STREAM_DATA* p = &data)
        {
            return PInvoke.FindNextStream(DangerousGetHandleForPInvoke(), p);
        }
    }

    // FindClose is the required release function for FindFirstStreamW/FindNextStreamW handles (NOT CloseHandle).
    // The SafeHandle contract guarantees this runs exactly once even under abort/finalization.
    protected override bool ReleaseHandle() => PInvoke.FindClose(new HANDLE(handle));
}
