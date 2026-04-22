namespace WinUiFileManager.Interop.Adapters;

internal interface IRestartManagerInterop
{
    int StartSession(out uint sessionHandle);

    int RegisterResources(uint sessionHandle, string[] resources);

    int GetList(
        uint sessionHandle,
        out uint processInfoNeeded,
        ref uint processInfo,
        RestartManagerProcessInfo[]? processInfos,
        out uint rebootReasons);

    int EndSession(uint sessionHandle);
}
