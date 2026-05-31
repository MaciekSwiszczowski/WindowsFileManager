using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

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
