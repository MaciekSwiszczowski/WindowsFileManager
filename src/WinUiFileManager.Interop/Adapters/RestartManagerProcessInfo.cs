namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Managed projection of a single Restart Manager <c>RM_PROCESS_INFO</c> entry: a process that holds a lock on a
/// registered file. Returned by <see cref="IRestartManagerInterop.GetList"/>. Immutable DTO with no native
/// resources.
/// </summary>
/// <param name="ProcessId">The locking process's PID (<c>0</c> if it could not be represented as an <see cref="int"/>).</param>
/// <param name="AppName">The friendly application name reported by the Restart Manager.</param>
/// <param name="ServiceShortName">The short service name when the locker is a service; otherwise empty.</param>
public sealed record RestartManagerProcessInfo(
    int ProcessId,
    string AppName,
    string ServiceShortName);
