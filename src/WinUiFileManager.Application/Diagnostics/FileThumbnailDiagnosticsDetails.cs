namespace WinUiFileManager.Application.Diagnostics;

/// <summary>
/// Immutable shell-thumbnail result for a file, shown in the inspector's Thumbnail section. Produced by
/// the Diagnostics layer in reply to
/// <see cref="WinUiFileManager.Application.Messages.RequestMessages.Inspector.InspectorDiagnosticsRequestMessage"/>.
/// </summary>
/// <remarks>
/// Carries decoded, top-down 32-bpp <b>BGRA</b> pixels plus their dimensions (the Win32 Shell imaging path returns
/// raw pixels, not an encoded stream). The buffer is a small (≈ edge² × 4) plain managed array — at the inspector's
/// 48px request it is ≈ 9 KB, comfortably on the SOH — so this type needs no pooling or disposal.
/// </remarks>
/// <param name="ThumbnailPixels">Top-down BGRA8 pixels (length = <paramref name="Height"/> × <paramref name="Width"/> × 4), or <see langword="null"/> when none is available.</param>
/// <param name="Width">Thumbnail pixel width, or <c>0</c> when there are no pixels.</param>
/// <param name="Height">Thumbnail pixel height, or <c>0</c> when there are no pixels.</param>
/// <param name="ProgId">The shell ProgID / extension association for the file, for diagnostics.</param>
public sealed record FileThumbnailDiagnosticsDetails(byte[]? ThumbnailPixels, int Width, int Height, string ProgId)
{
    /// <summary>Sentinel for "no thumbnail available" (null pixels, empty ProgID).</summary>
    public static FileThumbnailDiagnosticsDetails Empty { get; } = new(null, 0, 0, string.Empty);
}
