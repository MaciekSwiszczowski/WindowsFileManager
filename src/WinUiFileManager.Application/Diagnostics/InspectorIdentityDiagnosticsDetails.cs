namespace WinUiFileManager.Application.Diagnostics;

public sealed record InspectorIdentityDiagnosticsDetails(
    FileNtfsMetadataDetails NtfsMetadata,
    FileIdentityDetails Identity)
{
    public static InspectorIdentityDiagnosticsDetails Empty { get; } = new(
        new FileNtfsMetadataDetails(0, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue),
        new FileIdentityDetails(NtfsFileId.None, string.Empty, string.Empty, string.Empty, string.Empty));
}
