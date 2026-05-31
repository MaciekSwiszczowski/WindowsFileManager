namespace WinUiFileManager.Interop.Adapters;

public interface ICloudFilesInterop
{
    uint GetPlaceholderState(uint fileAttributes, uint reparseTag);
}
