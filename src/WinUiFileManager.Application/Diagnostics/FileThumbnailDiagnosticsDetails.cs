namespace WinUiFileManager.Application.Diagnostics;

/// <summary>
/// Immutable shell-thumbnail result for a file, shown in the inspector's Thumbnail section. Produced by
/// the Diagnostics layer in reply to
/// <see cref="WinUiFileManager.Application.Messages.RequestMessages.Inspector.InspectorDiagnosticsRequestMessage"/>.
/// </summary>
/// <param name="ThumbnailBytes">Encoded thumbnail image bytes, or <see langword="null"/> when none is available.</param>
/// <param name="ProgId">The shell ProgID of the handler that produced the thumbnail, for diagnostics.</param>
public sealed record FileThumbnailDiagnosticsDetails(
    byte[]? ThumbnailBytes,
    string ProgId)
{
    /// <summary>Sentinel for "no thumbnail available" (null bytes, empty ProgID).</summary>
    public static FileThumbnailDiagnosticsDetails Empty { get; } = new(null, string.Empty);
}
