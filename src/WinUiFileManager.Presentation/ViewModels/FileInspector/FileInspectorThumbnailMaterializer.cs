using System.Reactive.Concurrency;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class FileInspectorThumbnailMaterializer
{
    private readonly ISchedulerProvider _schedulers;
    private readonly FileInspectorFieldState _fieldState;
    private readonly ILogger<FileInspectorViewModel> _logger;
    private readonly Func<bool> _isDisposed;
    private readonly Func<bool> _hasCurrentSelection;
    private readonly Func<long> _currentSelectionVersion;
    private readonly Action _refreshVisibleCategories;

    public FileInspectorThumbnailMaterializer(
        ISchedulerProvider schedulers,
        FileInspectorFieldState fieldState,
        ILogger<FileInspectorViewModel> logger,
        Func<bool> isDisposed,
        Func<bool> hasCurrentSelection,
        Func<long> currentSelectionVersion,
        Action refreshVisibleCategories)
    {
        _schedulers = schedulers;
        _fieldState = fieldState;
        _logger = logger;
        _isDisposed = isDisposed;
        _hasCurrentSelection = hasCurrentSelection;
        _currentSelectionVersion = currentSelectionVersion;
        _refreshVisibleCategories = refreshVisibleCategories;
    }

    public async Task ApplyAsync(long selectionVersion, byte[]? thumbnailBytes)
    {
        if (!IsCurrentSelection(selectionVersion))
        {
            return;
        }

        if (thumbnailBytes is null || thumbnailBytes.Length == 0)
        {
            await RunOnMainThreadAsync(() =>
            {
                if (IsCurrentSelection(selectionVersion))
                {
                    _fieldState.SetThumbnailSource("Thumbnail", null);
                    _fieldState.SetLoading("Thumbnail", false);
                }

                return Task.CompletedTask;
            }).ConfigureAwait(false);
            return;
        }

        try
        {
            var stream = await Task.Run(
                () => CreateThumbnailStreamAsync(thumbnailBytes)).ConfigureAwait(false);

            await RunOnMainThreadAsync(async () =>
            {
                if (!IsCurrentSelection(selectionVersion))
                {
                    return;
                }

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                stream.Dispose();

                if (!IsCurrentSelection(selectionVersion))
                {
                    return;
                }

                _fieldState.SetThumbnailSource("Thumbnail", bitmap);
                _fieldState.SetValue("Thumbnail", "Preview");
                _fieldState.SetLoading("Thumbnail", false);
                _refreshVisibleCategories();
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to materialize thumbnail preview.");
            await RunOnMainThreadAsync(() =>
            {
                if (IsCurrentSelection(selectionVersion))
                {
                    _fieldState.SetThumbnailSource("Thumbnail", null);
                    _fieldState.SetLoading("Thumbnail", false);
                }

                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
    }

    private bool IsCurrentSelection(long selectionVersion) =>
        !_isDisposed() && _hasCurrentSelection() && selectionVersion == _currentSelectionVersion();

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

    private Task RunOnMainThreadAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _schedulers.MainThread.Schedule(() =>
        {
            _ = InvokeAsync();
        });

        return completion.Task;

        async Task InvokeAsync()
        {
            try
            {
                await action();
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }
    }
}
