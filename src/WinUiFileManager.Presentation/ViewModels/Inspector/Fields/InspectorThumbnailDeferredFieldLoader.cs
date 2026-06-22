using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUiFileManager.Application.Caching;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Thumbnails category. Requests thumbnail diagnostics via
/// <see cref="InspectorDiagnosticsRequestMessage"/>, converts the returned BGRA pixels into a
/// <see cref="SoftwareBitmapSource"/> (cached by content hash so repeated/identical thumbnails skip the conversion),
/// and applies both the image and the text fields.
/// </summary>
/// <remarks>
/// Image construction (<see cref="SoftwareBitmapSource.SetBitmapAsync"/>) is UI/WinRT-affine and runs in the apply
/// step. The conversion cache is owned per loader instance (one per inspector) and disposed with it; it dedupes the
/// <see cref="SoftwareBitmapSource"/> objects so a folder of one file type rebuilds the preview only once.
/// </remarks>
internal sealed class InspectorThumbnailDeferredFieldLoader :
    InspectorDeferredFieldLoaderBase<
        InspectorThumbnailDiagnosticsResponseMessage,
        FileThumbnailDiagnosticsDetails>
{
    private static readonly IReadOnlyList<string> ThumbnailFieldKeys =
    [
        "Thumbnail",
        "Has Thumbnail",
        "Association",
    ];

    private readonly ThumbnailConversionCache<SoftwareBitmapSource> _conversionCache = new();

    public InspectorThumbnailDeferredFieldLoader(
        IFileManagerMessenger messenger,
        SynchronizationContext uiSynchronizationContext,
        ILogger<InspectorThumbnailDeferredFieldLoader> logger)
        : base(messenger, uiSynchronizationContext, logger)
    {
    }

    protected override IReadOnlyList<string> FieldKeys => ThumbnailFieldKeys;

    [SuppressMessage(
        "IDisposableAnalyzers.Correctness",
        "IDISP001:Dispose created",
        Justification = "The SoftwareBitmapSource is owned by the per-loader conversion cache (disposed on eviction/disposal), not by this method; disposing it here would break the cache and the displayed image.")]
    protected override async Task ApplyAsync(FileThumbnailDiagnosticsDetails diagnostics)
    {
        var thumbnailSource = await GetOrCreateThumbnailSourceAsync(diagnostics).ConfigureAwait(true);
        FieldValueUpdater.ShowThumbnailDiagnostics(diagnostics, thumbnailSource);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _conversionCache.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Returns the cached image for these bytes, or converts and caches it; <c>null</c> when there are no pixels.</summary>
    private async Task<SoftwareBitmapSource?> GetOrCreateThumbnailSourceAsync(FileThumbnailDiagnosticsDetails diagnostics)
    {
        if (diagnostics.ThumbnailPixels is not { Length: > 0 } pixels
            || diagnostics.Width <= 0
            || diagnostics.Height <= 0)
        {
            return null;
        }

        var width = diagnostics.Width;
        var height = diagnostics.Height;
        // Key folds in width/height: byte-identical buffers can still differ in dimensions (RESIZETOFIT yields
        // non-square sizes), so pixels alone would let a wrong-shape image be served from the cache.
        var key = ThumbnailContentHash.Compute(pixels, width, height);
        return await _conversionCache
            .GetOrConvertAsync(key, () => CreateThumbnailSourceAsync(pixels, width, height))
            .ConfigureAwait(true);
    }

    /// <summary>
    /// Builds a <see cref="SoftwareBitmapSource"/> from top-down BGRA pixels. The intermediate
    /// <see cref="SoftwareBitmap"/> is disposed once <see cref="SoftwareBitmapSource.SetBitmapAsync"/> has copied it.
    /// </summary>
    private static async Task<SoftwareBitmapSource> CreateThumbnailSourceAsync(byte[] pixels, int width, int height)
    {
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            pixels.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            width,
            height,
            BitmapAlphaMode.Premultiplied);

        var source = new SoftwareBitmapSource();
        try
        {
            await source.SetBitmapAsync(bitmap);
            return source;
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }
}
