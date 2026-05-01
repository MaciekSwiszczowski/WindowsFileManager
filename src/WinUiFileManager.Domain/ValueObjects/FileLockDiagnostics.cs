namespace WinUiFileManager.Domain.ValueObjects;

public sealed record FileLockDiagnostics
{
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

    public bool? InUse { get; init; }

    public IReadOnlyList<string> LockBy { get; init; }

    public IReadOnlyList<int> LockPids { get; init; }

    public IReadOnlyList<string> LockServices { get; init; }
}
