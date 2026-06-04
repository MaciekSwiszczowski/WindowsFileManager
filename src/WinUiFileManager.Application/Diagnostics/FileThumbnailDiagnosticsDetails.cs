namespace WinUiFileManager.Application.Diagnostics;

/// <summary>
/// Immutable shell-thumbnail result for a file, shown in the inspector's Thumbnail section. Produced by
/// the Diagnostics layer in reply to
/// <see cref="WinUiFileManager.Application.Messages.RequestMessages.Inspector.InspectorDiagnosticsRequestMessage"/>.
/// </summary>
/// <remarks>
/// Single-owner: exactly one consumer reads <see cref="ThumbnailBytes"/> and then disposes this instance, which
/// returns the pooled buffer. The bytes are invalid after disposal, so additional subscribers must not retain or
/// read <see cref="ThumbnailBytes"/> past the owning consumer's disposal.
/// </remarks>
/// <param name="ThumbnailBytes">Owned encoded thumbnail image bytes, or <see langword="null"/> when none is available.</param>
/// <param name="ProgId">The shell ProgID of the handler that produced the thumbnail, for diagnostics.</param>
public sealed record FileThumbnailDiagnosticsDetails(
    PooledThumbnailBytes? ThumbnailBytes,
    string ProgId) : IDisposable
{
    /// <summary>Sentinel for "no thumbnail available" (null bytes, empty ProgID).</summary>
    public static FileThumbnailDiagnosticsDetails Empty { get; } = new(null, string.Empty);

    /// <summary>Returns owned thumbnail bytes to the shared pool, when present.</summary>
    public void Dispose() => ThumbnailBytes?.Dispose();
}
