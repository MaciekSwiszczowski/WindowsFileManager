namespace WinUiFileManager.Interop.Adapters;

internal sealed record ShellExecutePropertiesResult(
    bool Succeeded,
    int LastError,
    nint HInstApp);
