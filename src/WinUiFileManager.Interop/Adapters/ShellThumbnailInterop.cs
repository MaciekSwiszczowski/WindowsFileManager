using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// CsWin32-backed adapter that extracts a thumbnail straight from a path via the Win32 Shell imaging COM API
/// (<c>SHCreateItemFromParsingName</c> → <c>IShellItemImageFactory::GetImage</c> → <c>HBITMAP</c>), then copies
/// the pixels into a managed <see cref="ShellThumbnail"/>. This is the Interop-layer implementation of
/// <see cref="IShellThumbnailInterop"/> and the candidate replacement for the WinRT
/// <c>StorageFile.GetThumbnailAsync</c> path used by the inspector today.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> the WinRT thumbnail path creates several non-disposable runtime-callable wrappers per
/// selection (<c>StorageFile</c>, the async operation, the thumbnail stream) whose native COM is released only by
/// the GC finalizer, producing the RCW/finalizer churn measured in the StorageItem benchmarks. This path creates a
/// single <c>IShellItemImageFactory</c> RCW and releases it deterministically via
/// <see cref="Marshal.FinalReleaseComObject"/>; the <c>HBITMAP</c> comes back as a CsWin32
/// <c>DeleteObjectSafeHandle</c> that frees the GDI bitmap on dispose.
/// </para>
/// <para>
/// CsWin32 stays in its default marshaled-COM mode here (pointer-mode <c>allowMarshaling: false</c> would convert
/// the whole project's COM, including the existing <c>EnumDisplayMonitors</c> callback in
/// <c>WindowPlacementInterop</c>, to unmanaged function pointers — out of scope for this path).
/// </para>
/// <para>
/// <b>Threading:</b> the call is synchronous and can block on a slow Shell handler, so callers run it off the UI
/// thread. COM must be initialized on the calling thread; the CLR does this (MTA) for thread-pool threads.
/// </para>
/// </remarks>
internal sealed unsafe class ShellThumbnailInterop : IShellThumbnailInterop
{
    public bool TryGetThumbnail(string path, uint size, out ShellThumbnail thumbnail)
    {
        thumbnail = default;

        var createHr = PInvoke.SHCreateItemFromParsingName(path, null, typeof(IShellItemImageFactory).GUID, out var item);
        if (createHr.Failed || item is not IShellItemImageFactory factory)
        {
            return false;
        }

        try
        {
            var requested = new SIZE { cx = (int)size, cy = (int)size };
            // GetImage is generated as a throwing COM method (PreserveSig=false); a failing HRESULT — e.g. no
            // thumbnail available — surfaces as a COMException, which we treat as "no thumbnail".
            // SIIGBF_RESIZETOFIT (0): allow a real thumbnail or an icon fallback, scaled to fit the box — matches
            // the WinRT ThumbnailMode.SingleItem + ResizeThumbnail behavior the inspector uses today.
            factory.GetImage(requested, SIIGBF.SIIGBF_RESIZETOFIT, out var hbitmap);
            using (hbitmap)
            {
                if (hbitmap is null || hbitmap.IsInvalid)
                {
                    return false;
                }

                return TryCopyPixels(hbitmap, out thumbnail);
            }
        }
        catch (COMException)
        {
            return false;
        }
        finally
        {
            // Deterministic COM release: drop the RCW's native reference now instead of leaving it to the finalizer,
            // which is the whole reason this path exists versus WinRT StorageFile thumbnails.
            Marshal.FinalReleaseComObject(factory);
        }
    }

    /// <summary>
    /// Copies the <c>HBITMAP</c> into a tightly-packed, top-down 32-bpp BGRA buffer. <c>GetImage</c> only promises
    /// "an HBITMAP the caller must <c>DeleteObject</c>" — it does not guarantee a 32-bpp <c>CreateDIBSection</c>
    /// bitmap — so the direct-read fast path validates that before touching <c>bmBits</c>, and anything else falls
    /// back to <c>GetDIBits</c>.
    /// </summary>
    private static bool TryCopyPixels(SafeHandle bitmap, out ShellThumbnail thumbnail)
    {
        thumbnail = default;

        DIBSECTION dib = default;
        var read = PInvoke.GetObject(new HGDIOBJ(bitmap.DangerousGetHandle()), sizeof(DIBSECTION), &dib);
        GC.KeepAlive(bitmap);
        // GetObject fills the leading BITMAP for any HBITMAP, but only fills the full DIBSECTION (including bmBits)
        // for a CreateDIBSection-backed bitmap. Below sizeof(BITMAP) there is nothing usable.
        if (read < sizeof(BITMAP) || dib.dsBm.bmWidth <= 0 || dib.dsBm.bmHeight <= 0)
        {
            return false;
        }

        var width = dib.dsBm.bmWidth;
        var height = dib.dsBm.bmHeight;
        var destinationStride = width * 4;
        var pixels = new byte[height * destinationStride];

        // Fast path: a real 32-bpp DIB section whose bits we can read directly, which preserves the premultiplied
        // alpha. Guard every assumption the Win32 contract does not make: full DIBSECTION returned, real bit
        // pointer, exactly 32 bpp, and a source stride that actually covers the bytes we copy.
        var isReadableDib =
            read >= sizeof(DIBSECTION)
            && dib.dsBm.bmBits != null
            && dib.dsBm.bmBitsPixel == 32
            && dib.dsBm.bmWidthBytes >= destinationStride;

        if (isReadableDib)
        {
            // GetImage normally returns a top-down DIB (negative biHeight); honor either orientation so the copied
            // buffer is always top-down (row 0 = top) for the UI hop.
            CopyDibSection(
                (byte*)dib.dsBm.bmBits,
                dib.dsBm.bmWidthBytes,
                topDown: dib.dsBmih.biHeight < 0,
                pixels,
                height,
                destinationStride);
        }
        else if (!TryConvertWithGetDiBits(bitmap, pixels, width, height))
        {
            return false;
        }

        thumbnail = new ShellThumbnail(pixels, width, height, destinationStride);
        return true;
    }

    /// <summary>Copies a validated 32-bpp DIB section's bits row-by-row into the top-down destination buffer.</summary>
    private static void CopyDibSection(byte* source, int sourceStride, bool topDown, byte[] pixels, int height, int destinationStride)
    {
        for (var row = 0; row < height; row++)
        {
            var sourceRow = topDown ? row : height - 1 - row;
            var sourceLine = new ReadOnlySpan<byte>(source + (sourceRow * sourceStride), destinationStride);
            sourceLine.CopyTo(pixels.AsSpan(row * destinationStride, destinationStride));
        }
    }

    /// <summary>
    /// Converts any HBITMAP (including a device-dependent bitmap with no accessible bits) into the top-down 32-bpp
    /// destination buffer via <c>GetDIBits</c>. GDI does not carry alpha for <c>BI_RGB</c>, so the result is forced
    /// opaque — a visible fallback thumbnail rather than a fully transparent one.
    /// </summary>
    private static bool TryConvertWithGetDiBits(SafeHandle bitmap, byte[] pixels, int width, int height)
    {
        var info = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = width,
                biHeight = -height, // negative => top-down (row 0 = top)
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0, // BI_RGB: uncompressed
            },
        };

        // CreateCompatibleDC returns a plain HDC value (not a SafeHandle), so it is released explicitly via DeleteDC.
        var dc = PInvoke.CreateCompatibleDC(default);
        if (dc.IsNull)
        {
            return false;
        }

        try
        {
            int scanLines;
            fixed (byte* destination = pixels)
            {
                scanLines = PInvoke.GetDIBits(
                    dc,
                    new HBITMAP(bitmap.DangerousGetHandle()),
                    0,
                    (uint)height,
                    destination,
                    &info,
                    DIB_USAGE.DIB_RGB_COLORS);
            }

            GC.KeepAlive(bitmap);

            if (scanLines != height)
            {
                return false;
            }

            // GDI leaves the alpha byte undefined for BI_RGB; force opaque so the fallback thumbnail is visible.
            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 0xFF;
            }

            return true;
        }
        finally
        {
            PInvoke.DeleteDC(dc);
        }
    }
}
