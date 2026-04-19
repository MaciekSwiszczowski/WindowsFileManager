namespace WinUiFileManager.Domain.ValueObjects;

public sealed record FileLockDiagnostics
{
    public static readonly FileLockDiagnostics None = new(
        inUse: null,
        lockBy: [],
        lockPids: [],
        lockServices: [],
        usage: null,
        canSwitchTo: null,
        canClose: null);

    public FileLockDiagnostics(
        bool? inUse,
        IReadOnlyList<string> lockBy,
        IReadOnlyList<int> lockPids,
        IReadOnlyList<string> lockServices,
        string? usage,
        bool? canSwitchTo,
        bool? canClose)
    {
        InUse = inUse;
        LockBy = lockBy;
        LockPids = lockPids;
        LockServices = lockServices;
        Usage = usage;
        CanSwitchTo = canSwitchTo;
        CanClose = canClose;
    }

    public bool? InUse { get; init; }

    public IReadOnlyList<string> LockBy { get; init; }

    public IReadOnlyList<int> LockPids { get; init; }

    public IReadOnlyList<string> LockServices { get; init; }

    public string? Usage { get; init; }

    public bool? CanSwitchTo { get; init; }

    public bool? CanClose { get; init; }
}
