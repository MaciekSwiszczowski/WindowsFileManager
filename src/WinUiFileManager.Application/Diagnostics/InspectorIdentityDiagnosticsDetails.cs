namespace WinUiFileManager.Application.Diagnostics;

/// <summary>
/// Composite payload for the inspector's identity section, bundling the raw NTFS metadata with the
/// resolved identity facts. Produced by the Diagnostics layer in reply to
/// <see cref="WinUiFileManager.Application.Messages.RequestMessages.Inspector.InspectorIdentityDiagnosticsRequestMessage"/>.
/// </summary>
public sealed record InspectorIdentityDiagnosticsDetails(
    FileNtfsMetadataDetails NtfsMetadata,
    FileIdentityDetails Identity)
{
    /// <summary>Sentinel with zeroed metadata and <see cref="NtfsFileId.None"/> identity, used before/while loading.</summary>
    public static InspectorIdentityDiagnosticsDetails Empty { get; } = new(
        new FileNtfsMetadataDetails(0, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue),
        new FileIdentityDetails(NtfsFileId.None, string.Empty, string.Empty, string.Empty, string.Empty));
}
