using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Thumbnails category. Requests thumbnail diagnostics via
/// <see cref="InspectorThumbnailDiagnosticsRequestMessage"/>, decodes the returned bytes into a
/// <see cref="BitmapImage"/>, and applies both the image and the text fields.
/// </summary>
/// <remarks>Image decoding (<see cref="BitmapImage.SetSourceAsync"/>) is UI/WinRT-affine and runs in the apply step.</remarks>
internal sealed class InspectorThumbnailDeferredFieldLoader : InspectorDeferredFieldLoaderBase<FileThumbnailDiagnosticsDetails>
{
    private static readonly IReadOnlyList<string> ThumbnailFieldKeys =
    [
        "Thumbnail",
        "Has Thumbnail",
        "Association",
    ];

    private readonly IMessenger _messenger;

    public InspectorThumbnailDeferredFieldLoader(IMessenger messenger)
    {
        _messenger = messenger;
    }

    protected override IReadOnlyList<string> FieldKeys => ThumbnailFieldKeys;

    protected override async Task<FileThumbnailDiagnosticsDetails> LoadDiagnosticsAsync(
        NormalizedPath path,
        CancellationToken cancellationToken)
    {
        var request = _messenger.Send(new InspectorThumbnailDiagnosticsRequestMessage(path, cancellationToken));
        return request.HasReceivedResponse
            ? await request.Response
            : FileThumbnailDiagnosticsDetails.Empty;
    }

    protected override async Task ApplyAsync(FileThumbnailDiagnosticsDetails diagnostics)
    {
        var thumbnailSource = await CreateThumbnailSourceAsync(diagnostics.ThumbnailBytes);
        FieldValueUpdater.ShowThumbnailDiagnostics(diagnostics, thumbnailSource);
    }

    /// <summary>Decodes raw thumbnail bytes into a <see cref="BitmapImage"/>, or returns <c>null</c> when there are none.</summary>
    private static async Task<BitmapImage?> CreateThumbnailSourceAsync(byte[]? thumbnailBytes)
    {
        if (thumbnailBytes is null || thumbnailBytes.Length == 0)
        {
            return null;
        }

        using var stream = await CreateThumbnailStreamAsync(thumbnailBytes);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    /// <summary>
    /// Wraps the bytes in a seek-reset <see cref="InMemoryRandomAccessStream"/> for <see cref="BitmapImage"/>.
    /// Disposes the stream if writing fails so a partial stream isn't leaked; the caller owns the returned stream.
    /// </summary>
    private static async Task<InMemoryRandomAccessStream> CreateThumbnailStreamAsync(byte[] thumbnailBytes)
    {
        var stream = new InMemoryRandomAccessStream();
        try
        {
            using var writer = new DataWriter();
            writer.WriteBytes(thumbnailBytes);
            var buffer = writer.DetachBuffer();
            await stream.WriteAsync(buffer);
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
