using Windows.Win32;
using Windows.Win32.Storage.CloudFilters;

namespace WinUiFileManager.Interop.Adapters;

internal sealed class CloudFilesInterop : ICloudFilesInterop
{
    public uint GetPlaceholderState(uint fileAttributes, uint reparseTag)
    {
        return GetPlaceholderStateCore(
            static (attributes, tag) => PInvoke.CfGetPlaceholderStateFromAttributeTag(attributes, tag),
            fileAttributes,
            reparseTag);
    }

    internal static uint GetPlaceholderStateCore(
        Func<uint, uint, CF_PLACEHOLDER_STATE> getPlaceholderState,
        uint fileAttributes,
        uint reparseTag)
    {
        return (uint)getPlaceholderState(fileAttributes, reparseTag);
    }
}
