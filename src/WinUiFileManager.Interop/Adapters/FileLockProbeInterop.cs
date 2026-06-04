using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// CsWin32-backed probe that tests whether a file can be opened with no sharing before the diagnostics layer pays
/// for Restart Manager owner discovery.
/// </summary>
public sealed class FileLockProbeInterop
{
    private const int ErrorSuccess = 0;
    private const uint FileReadDataAccess = 0x0001;

    /// <summary>
    /// Attempts to open <paramref name="path"/> with <see cref="FileShare.None"/>.
    /// </summary>
    /// <param name="path">Existing file path to probe.</param>
    /// <returns>Win32 error code; <c>0</c> means the exclusive open succeeded.</returns>
    public int TryOpenExclusively(string path)
    {
        using var handle = PInvoke.CreateFile(
            path,
            FileReadDataAccess,
            FILE_SHARE_MODE.FILE_SHARE_NONE,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL);

        return handle.IsInvalid
            ? Marshal.GetLastPInvokeError()
            : ErrorSuccess;
    }
}
