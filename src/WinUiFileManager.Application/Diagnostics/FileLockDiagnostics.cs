namespace WinUiFileManager.Application.Diagnostics;

/// <summary>
/// Immutable result describing which processes/services currently hold a file open (via the Restart
/// Manager), shown in the inspector's Locks section. Produced by the Diagnostics layer in reply to
/// <see cref="WinUiFileManager.Application.Messages.RequestMessages.Inspector.InspectorDiagnosticsRequestMessage"/>.
/// </summary>
public sealed record FileLockDiagnostics
{
    /// <summary>Sentinel for "no lock information" (<see cref="InUse"/> is null and all lists are empty).</summary>
    public static readonly FileLockDiagnostics None = new(
        inUse: null,
        lockBy: [],
        lockPids: [],
        lockServices: []);

    public FileLockDiagnostics(
        bool? inUse,
        IReadOnlyList<string> lockBy,
        IReadOnlyList<int> lockPids,
        IReadOnlyList<string> lockServices)
    {
        InUse = inUse;
        LockBy = lockBy;
        LockPids = lockPids;
        LockServices = lockServices;
    }

    /// <summary>Whether the file is currently in use; <see langword="null"/> when the lock state could not be determined.</summary>
    public bool? InUse { get; init; }

    /// <summary>Display names of the applications/processes holding the file.</summary>
    public IReadOnlyList<string> LockBy { get; init; }

    /// <summary>Process ids of the holders, aligned conceptually with <see cref="LockBy"/>.</summary>
    public IReadOnlyList<int> LockPids { get; init; }

    /// <summary>Names of Windows services holding the file (Restart Manager reports these separately).</summary>
    public IReadOnlyList<string> LockServices { get; init; }
}
