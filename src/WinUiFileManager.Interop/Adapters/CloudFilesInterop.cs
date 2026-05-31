using Windows.Win32;
using Windows.Win32.Storage.CloudFilters;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Adapter over the Cloud Filter API (<c>CfGetPlaceholderStateFromAttributeTag</c>) used to classify whether a
/// file is a cloud placeholder (e.g. OneDrive "Files On-Demand") and, if so, its sync/hydration state.
/// Implements <see cref="ICloudFilesInterop"/>; this is the only place the <c>CldApi</c> binding is touched.
/// </summary>
internal sealed class CloudFilesInterop : ICloudFilesInterop
{
    /// <summary>
    /// Computes the cloud placeholder state from already-known file attributes and reparse tag, avoiding any
    /// additional file open or I/O.
    /// </summary>
    /// <param name="fileAttributes">The file's <c>WIN32_FILE_ATTRIBUTE_DATA</c> attribute flags.</param>
    /// <param name="reparseTag">The file's reparse tag (<c>0</c> if it has none).</param>
    /// <returns>The <c>CF_PLACEHOLDER_STATE</c> bit flags as a <see cref="uint"/>.</returns>
    public uint GetPlaceholderState(uint fileAttributes, uint reparseTag)
    {
        return GetPlaceholderStateCore(
            static (attributes, tag) => PInvoke.CfGetPlaceholderStateFromAttributeTag(attributes, tag),
            fileAttributes,
            reparseTag);
    }

    // Test seam: keeps the enum-to-uint marshalling testable against a fake without P/Invoking CldApi.
    internal static uint GetPlaceholderStateCore(
        Func<uint, uint, CF_PLACEHOLDER_STATE> getPlaceholderState,
        uint fileAttributes,
        uint reparseTag)
    {
        return (uint)getPlaceholderState(fileAttributes, reparseTag);
    }
}
