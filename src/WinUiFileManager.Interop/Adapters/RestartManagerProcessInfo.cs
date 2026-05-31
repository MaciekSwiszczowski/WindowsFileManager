namespace WinUiFileManager.Interop.Adapters;

public sealed record RestartManagerProcessInfo(
    int ProcessId,
    string AppName,
    string ServiceShortName);
