namespace WinUiFileManager.Interop.Types;

/// <summary>
/// An extracted Shell thumbnail as a managed, decoded pixel buffer. Produced by
/// <see cref="WinUiFileManager.Interop.Adapters.IShellThumbnailInterop"/> from the Win32 Shell imaging path
/// (<c>IShellItemImageFactory::GetImage</c>), as opposed to the WinRT <c>StorageFile.GetThumbnailAsync</c> path
/// which returns an encoded image stream.
/// </summary>
/// <remarks>
/// <para>
/// Pixels are 32-bit <b>BGRA</b> (byte order B, G, R, A), top-down (row 0 is the top), tightly packed so that
/// <see cref="Stride"/> equals <see cref="Width"/> × 4. This is the layout a later UI hop can feed straight into
/// a <c>SoftwareBitmap</c> (BGRA8) → <c>SoftwareBitmapSource</c>; the alpha channel is premultiplied as returned
/// by the Shell.
/// </para>
/// <para>
/// This is a plain managed value (the buffer is a normal GC array, not a native handle), so it needs no disposal.
/// The native <c>HBITMAP</c> it was copied from is released inside the adapter before this value is returned.
/// </para>
/// </remarks>
public readonly struct ShellThumbnail
{
    /// <summary>Creates a thumbnail value over an already-copied, tightly-packed BGRA pixel buffer.</summary>
    /// <param name="pixels">Top-down BGRA8 pixels; length must be <paramref name="height"/> × <paramref name="stride"/>.</param>
    /// <param name="width">Pixel width.</param>
    /// <param name="height">Pixel height.</param>
    /// <param name="stride">Bytes per row (<paramref name="width"/> × 4).</param>
    internal ShellThumbnail(byte[] pixels, int width, int height, int stride)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
        Stride = stride;
    }

    /// <summary>Top-down BGRA8 pixel bytes, <see cref="Height"/> × <see cref="Stride"/> long.</summary>
    public byte[] Pixels { get; }

    /// <summary>Pixel width.</summary>
    public int Width { get; }

    /// <summary>Pixel height.</summary>
    public int Height { get; }

    /// <summary>Bytes per row; equals <see cref="Width"/> × 4.</summary>
    public int Stride { get; }
}
