namespace WinUiFileManager.Interop.Types;

public sealed record FileLockDiagnosticsResult
{
    public FileLockDiagnosticsResult(
        bool success,
        bool? inUse,
        IReadOnlyList<string> lockBy,
        IReadOnlyList<int> lockPids,
        IReadOnlyList<string> lockServices,
        string? usage,
        bool? canSwitchTo,
        bool? canClose,
        string? errorMessage)
    {
        Success = success;
        InUse = inUse;
        LockBy = lockBy;
        LockPids = lockPids;
        LockServices = lockServices;
        Usage = usage;
        CanSwitchTo = canSwitchTo;
        CanClose = canClose;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; init; }

    public bool? InUse { get; init; }

    public IReadOnlyList<string> LockBy { get; init; }

    public IReadOnlyList<int> LockPids { get; init; }

    public IReadOnlyList<string> LockServices { get; init; }

    public string? Usage { get; init; }

    public bool? CanSwitchTo { get; init; }

    public bool? CanClose { get; init; }

    public string? ErrorMessage { get; init; }
}
