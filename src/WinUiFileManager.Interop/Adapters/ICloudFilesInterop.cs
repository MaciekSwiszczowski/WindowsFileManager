namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Abstraction over the Cloud Filter placeholder-state query. Implemented by <see cref="CloudFilesInterop"/>;
/// lets Infrastructure classify cloud/placeholder files without referencing <c>Windows.Win32.*</c>.
/// </summary>
public interface ICloudFilesInterop
{
    /// <summary>
    /// Returns the <c>CF_PLACEHOLDER_STATE</c> flags (as a <see cref="uint"/>) computed purely from the supplied
    /// attributes and reparse tag — no file open required.
    /// </summary>
    /// <param name="fileAttributes">File attribute flags.</param>
    /// <param name="reparseTag">Reparse tag, or <c>0</c> if none.</param>
    uint GetPlaceholderState(uint fileAttributes, uint reparseTag);
}
