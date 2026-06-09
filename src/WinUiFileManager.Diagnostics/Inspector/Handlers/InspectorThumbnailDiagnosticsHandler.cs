using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Diagnostics.Profiling;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Diagnostics.Inspector;

namespace WinUiFileManager.Diagnostics.Inspector.Handlers;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorDiagnosticsRequestMessage"/> by
/// fetching a Shell thumbnail (via WinRT <see cref="StorageFile"/>/<see cref="StorageFolder"/>) for the
/// requested path and returning it as raw bytes plus the file's ProgID/extension.
/// </summary>
/// <remarks>
/// <see cref="LoadAsync"/> begins on a thread-pool thread and awaits the WinRT Storage/thumbnail APIs with
/// <c>ConfigureAwait(false)</c>. These particular WinRT APIs are usable off the UI/STA thread; note that other
/// WinRT/STA-bound APIs are <i>not</i> — this handler stays off the UI thread because thumbnail extraction can be slow.
/// </remarks>
public sealed class InspectorThumbnailDiagnosticsHandler :
    InspectorDiagnosticsHandlerBase<
        FileThumbnailDiagnosticsDetails,
        InspectorThumbnailDiagnosticsResponseMessage>
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(5);
    private const uint ThumbnailSize = 256;
    private const int MaxThumbnailBytes = 4 * 1024 * 1024;

    public InspectorThumbnailDiagnosticsHandler(
        IMessenger messenger,
        ILogger<InspectorThumbnailDiagnosticsHandler> logger,
        Func<FileThumbnailDiagnosticsDetails, InspectorThumbnailDiagnosticsResponseMessage> responseFactory,
        IInspectorDiagnosticsGate diagnosticsGate)
        : base(messenger, logger, responseFactory, diagnosticsGate)
    {
    }

    protected override DiagnosticsCategory Category => DiagnosticsCategory.Thumbnail;

    /// <summary>
    /// Retrieves a 256px single-item thumbnail for the path and copies it into a byte array.
    /// </summary>
    /// <param name="message">The request carrying the target path.</param>
    /// <returns>
    /// Thumbnail bytes (or null bytes with just the ProgID when none is available), or
    /// <see cref="FileThumbnailDiagnosticsDetails.Empty"/> on failure.
    /// </returns>
    /// <remarks>Thread-pool bound. Errors are logged and degraded to empty by the base class.</remarks>
    protected override async Task<FileThumbnailDiagnosticsDetails> LoadAsync(InspectorDiagnosticsRequestMessage message)
    {
        using var timeoutCts = new CancellationTokenSource();
        timeoutCts.CancelAfter(LoadTimeout);

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

    protected override FileThumbnailDiagnosticsDetails GetEmptyDiagnostics(InspectorDiagnosticsRequestMessage request) =>
        FileThumbnailDiagnosticsDetails.Empty;

    private static async Task<PooledThumbnailBytes> CopyThumbnailBytesAsync(StorageItemThumbnail thumbnail, CancellationToken cancellationToken)
    {
        var expectedSize = checked((int)thumbnail.Size);
        var buffer = PooledThumbnailBytes.Rent(expectedSize);
        thumbnail.Seek(0);
        try
        {
            var input = thumbnail.AsStreamForRead();
            await using (input.ConfigureAwait(false))
            {
                await buffer.FillFromAsync(input, cancellationToken).ConfigureAwait(false);
                return buffer;
            }
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
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
