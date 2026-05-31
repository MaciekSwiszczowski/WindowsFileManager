using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Diagnostics.Inspector;

public sealed class InspectorThumbnailDiagnosticsHandler : IDisposable
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<InspectorThumbnailDiagnosticsHandler> _logger;
    private readonly IMessenger _messenger;
    private bool _disposed;

    public InspectorThumbnailDiagnosticsHandler(
        IMessenger messenger,
        ILogger<InspectorThumbnailDiagnosticsHandler> logger)
    {
        _messenger = messenger;
        _logger = logger;
    }

    public void Initialize()
    {
        _messenger.Register<InspectorThumbnailDiagnosticsRequestMessage>(this,
            (_, message) => message.Reply(Task.Run(() => LoadAsync(message))));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    private async Task<FileThumbnailDiagnosticsDetails> LoadAsync(InspectorThumbnailDiagnosticsRequestMessage message)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(message.CancellationToken);
        timeoutCts.CancelAfter(LoadTimeout);

        try
        {
            var path = message.Path.DisplayPath;
            var progId = Path.GetExtension(path);
            var storageItem = await TryGetStorageItemAsync(path).ConfigureAwait(false);
            if (storageItem is null)
            {
                return new FileThumbnailDiagnosticsDetails(null, progId);
            }

            using var thumbnail = storageItem is StorageFile file
                ? await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 256).AsTask(timeoutCts.Token).ConfigureAwait(false)
                : await ((StorageFolder)storageItem).GetThumbnailAsync(ThumbnailMode.SingleItem, 256).AsTask(timeoutCts.Token).ConfigureAwait(false);

            if (thumbnail is null)
            {
                return new FileThumbnailDiagnosticsDetails(null, progId);
            }

            thumbnail.Seek(0);
            using var input = thumbnail.AsStreamForRead();
            using var output = new MemoryStream();
            await input.CopyToAsync(output, timeoutCts.Token).ConfigureAwait(false);
            return new FileThumbnailDiagnosticsDetails(output.ToArray(), progId);
        }
        catch (OperationCanceledException) when (message.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load inspector thumbnail diagnostics for {Path}", message.Path.DisplayPath);
            return FileThumbnailDiagnosticsDetails.Empty;
        }
    }

    private static async Task<IStorageItem?> TryGetStorageItemAsync(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return await StorageFile.GetFileFromPathAsync(path);
            }

            if (Directory.Exists(path))
            {
                return await StorageFolder.GetFolderFromPathAsync(path);
            }
        }
        catch
        {
        }

        return null;
    }
}
