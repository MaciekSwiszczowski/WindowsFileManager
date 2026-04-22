namespace WinUiFileManager.Interop.Adapters;

internal interface ICloudFilesInterop
{
    uint GetPlaceholderState(uint fileAttributes, uint reparseTag);
}
