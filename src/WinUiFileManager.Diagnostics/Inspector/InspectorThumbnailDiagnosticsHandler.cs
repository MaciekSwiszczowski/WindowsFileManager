using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorThumbnailDiagnosticsRequestMessage"/> by
/// fetching a Shell thumbnail (via WinRT <see cref="StorageFile"/>/<see cref="StorageFolder"/>) for the
/// requested path and returning it as raw bytes plus the file's ProgID/extension.
/// </summary>
/// <remarks>
/// Lifetime: DI singleton; registers in <see cref="Initialize"/>, unregisters in <see cref="Dispose"/>,
/// which is effectively unreachable because the container is never disposed (AGENTS.md §5).
/// Threading: answered via <c>message.Reply(Task.Run(...))</c>, so <see cref="LoadAsync"/> begins on a
/// thread-pool thread and awaits the WinRT Storage/thumbnail APIs with <c>ConfigureAwait(false)</c>
/// (library convention, AGENTS.md §6). These particular WinRT APIs are usable off the UI/STA thread; note
/// that other WinRT/STA-bound APIs are <i>not</i> (AGENTS.md §5) — this handler stays off the UI thread on
/// purpose because thumbnail extraction can be slow.
/// </remarks>
public sealed class InspectorThumbnailDiagnosticsHandler : IDisposable
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(5);
    private const uint ThumbnailSize = 256;
    private const int MaxThumbnailBytes = 4 * 1024 * 1024;

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

    /// <summary>Registers the request handler. Not idempotent — call exactly once (AGENTS.md §4).</summary>
    public void Initialize()
    {
        _messenger.Register<InspectorThumbnailDiagnosticsRequestMessage>(this,
            (_, message) => message.Reply(Task.Run(() => LoadAsync(message))));
    }

    /// <summary>Unregisters from the messenger (idempotent). See type remarks: effectively never called.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    /// <summary>
    /// Retrieves a 256px single-item thumbnail for the path and copies it into a byte array.
    /// </summary>
    /// <param name="message">The request carrying the target path and cancellation token.</param>
    /// <returns>
    /// Thumbnail bytes (or null bytes with just the ProgID when none is available), or
    /// <see cref="FileThumbnailDiagnosticsDetails.Empty"/> on failure.
    /// </returns>
    /// <remarks>Thread-pool bound. Real cancellation is rethrown; other errors are logged and degraded to empty.</remarks>
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
                ? await file.GetThumbnailAsync(ThumbnailMode.SingleItem, ThumbnailSize).AsTask(timeoutCts.Token).ConfigureAwait(false)
                : await ((StorageFolder)storageItem).GetThumbnailAsync(ThumbnailMode.SingleItem, ThumbnailSize).AsTask(timeoutCts.Token).ConfigureAwait(false);

            if (thumbnail is null || thumbnail.Size == 0 || thumbnail.Size > MaxThumbnailBytes)
            {
                return new FileThumbnailDiagnosticsDetails(null, progId);
            }

            return new FileThumbnailDiagnosticsDetails(
                await CopyThumbnailBytesAsync(thumbnail, timeoutCts.Token).ConfigureAwait(false),
                progId);
        }
        catch (OperationCanceledException) when (message.CancellationToken.IsCancellationRequested)
        {
            // Rethrow only for genuine caller cancellation; timeout cancellation degrades to empty below.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load inspector thumbnail diagnostics for {Path}", message.Path.DisplayPath);
            return FileThumbnailDiagnosticsDetails.Empty;
        }
    }

    private static async Task<byte[]> CopyThumbnailBytesAsync(StorageItemThumbnail thumbnail, CancellationToken cancellationToken)
    {
        thumbnail.Seek(0);
        await using var input = thumbnail.AsStreamForRead();
        using var output = new MemoryStream((int)thumbnail.Size);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        return output.ToArray();
    }

    /// <summary>
    /// Opens the path as a WinRT <see cref="StorageFile"/> or <see cref="StorageFolder"/>, returning null
    /// if it is neither or cannot be opened.
    /// </summary>
    /// <remarks>Best-effort: all failures are swallowed so the caller simply gets "no thumbnail".</remarks>
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
            // ignored
        }

        return null;
    }
}
