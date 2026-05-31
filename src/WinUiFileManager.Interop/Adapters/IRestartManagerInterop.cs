namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Abstraction over the Windows Restart Manager session lifecycle used to determine which processes lock a file.
/// Implemented by <see cref="RestartManagerInterop"/>. All methods return the raw Win32 result code
/// (<c>0</c> = success) rather than throwing.
/// </summary>
/// <remarks>
/// Usage protocol: <see cref="StartSession"/> → <see cref="RegisterResources"/> → <see cref="GetList"/>
/// (count-probe then buffered) → <see cref="EndSession"/>. <see cref="EndSession"/> is mandatory to release the
/// OS session.
/// </remarks>
public interface IRestartManagerInterop
{
    /// <summary>Opens a session. <paramref name="sessionHandle"/> receives the handle. Must be closed via <see cref="EndSession"/>.</summary>
    int StartSession(out uint sessionHandle);

    /// <summary>Registers file paths to analyze for locking processes.</summary>
    int RegisterResources(uint sessionHandle, string[] resources);

    /// <summary>
    /// Enumerates locking processes. Call with <see langword="null"/>/empty <paramref name="processInfos"/> to get
    /// the required count via <paramref name="processInfoNeeded"/>, then again with a correctly-sized buffer.
    /// </summary>
    /// <param name="processInfo">In: buffer capacity. Out: number of entries written.</param>
    /// <param name="rebootReasons">Receives the reboot-reason flags.</param>
    int GetList(
        uint sessionHandle,
        out uint processInfoNeeded,
        ref uint processInfo,
        RestartManagerProcessInfo[]? processInfos,
        out uint rebootReasons);

    /// <summary>Closes the session opened by <see cref="StartSession"/>.</summary>
    int EndSession(uint sessionHandle);
}
