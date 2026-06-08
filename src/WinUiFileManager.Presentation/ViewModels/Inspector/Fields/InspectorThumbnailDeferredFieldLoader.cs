using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Thumbnails category. Requests thumbnail diagnostics via
/// <see cref="InspectorDiagnosticsRequestMessage"/>, decodes the returned bytes into a
/// <see cref="BitmapImage"/>, and applies both the image and the text fields.
/// </summary>
/// <remarks>Image decoding (<see cref="BitmapImage.SetSourceAsync"/>) is UI/WinRT-affine and runs in the apply step.</remarks>
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

    public InspectorThumbnailDeferredFieldLoader(
        IFileManagerMessenger messenger,
        SynchronizationContext uiSynchronizationContext,
        ILogger<InspectorThumbnailDeferredFieldLoader> logger)
        : base(messenger, uiSynchronizationContext, logger)
    {
    }

    protected override IReadOnlyList<string> FieldKeys => ThumbnailFieldKeys;

    protected override async Task ApplyAsync(FileThumbnailDiagnosticsDetails diagnostics)
    {
        var thumbnailSource = await CreateThumbnailSourceAsync(diagnostics.ThumbnailBytes).ConfigureAwait(true);
        FieldValueUpdater.ShowThumbnailDiagnostics(diagnostics, thumbnailSource);
    }

    /// <summary>Decodes owned thumbnail bytes into a <see cref="BitmapImage"/>, or returns <c>null</c> when there are none.</summary>
    private static async Task<BitmapImage?> CreateThumbnailSourceAsync(PooledThumbnailBytes? thumbnailBytes)
    {
        if (thumbnailBytes is null || thumbnailBytes.Length == 0)
        {
            return null;
        }

        using var stream = await CreateThumbnailStreamAsync(thumbnailBytes).ConfigureAwait(true);
        var bitmap = new BitmapImage { DecodePixelWidth = 256 };

        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    /// <summary>
    /// Wraps the owned bytes in a seek-reset <see cref="InMemoryRandomAccessStream"/> for <see cref="BitmapImage"/>.
    /// Writes the pooled buffer directly (single copy into the stream) and disposes the stream if writing fails so a
    /// partial stream isn't leaked; the caller owns the returned stream.
    /// </summary>
    private static async Task<InMemoryRandomAccessStream> CreateThumbnailStreamAsync(PooledThumbnailBytes thumbnailBytes)
    {
        var stream = new InMemoryRandomAccessStream();
        try
        {
            var segment = thumbnailBytes.Segment;
            await stream.WriteAsync(segment.Array!.AsBuffer(segment.Offset, segment.Count));
            stream.Seek(0);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }
}
