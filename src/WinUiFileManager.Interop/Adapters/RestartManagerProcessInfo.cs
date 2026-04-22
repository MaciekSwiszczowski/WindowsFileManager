namespace WinUiFileManager.Interop.Adapters;

internal sealed record RestartManagerProcessInfo(
    int ProcessId,
    string AppName,
    string ServiceShortName);
