namespace WinUiFileManager.Interop.Adapters;

internal interface IShellInterop
{
    bool ShowObjectProperties(string objectName, out int lastError);

    bool TryInitializeStaCom();

    void UninitializeCom();

    ShellExecutePropertiesResult ExecutePropertiesVerb(string objectName);
}
