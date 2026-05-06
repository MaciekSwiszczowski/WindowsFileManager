using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class FileInspectorThumbnailMaterializer
{
    private readonly FileInspectorFieldState _fieldState;
    private readonly ILogger<FileInspectorViewModel> _logger;
    private readonly Func<bool> _isDisposed;
    private readonly Func<bool> _hasCurrentSelection;
    private readonly Func<long> _currentSelectionVersion;
    private readonly Action _refreshVisibleCategories;

    public FileInspectorThumbnailMaterializer(
        FileInspectorFieldState fieldState,
        ILogger<FileInspectorViewModel> logger,
        Func<bool> isDisposed,
        Func<bool> hasCurrentSelection,
        Func<long> currentSelectionVersion,
        Action refreshVisibleCategories)
    {
        _fieldState = fieldState;
        _logger = logger;
        _isDisposed = isDisposed;
        _hasCurrentSelection = hasCurrentSelection;
        _currentSelectionVersion = currentSelectionVersion;
        _refreshVisibleCategories = refreshVisibleCategories;
    }

    public async Task ApplyAsync(long selectionVersion, byte[]? thumbnailBytes)
    {
        if (_isDisposed() || !_hasCurrentSelection() || selectionVersion != _currentSelectionVersion())
        {
            return;
        }

        if (thumbnailBytes is null || thumbnailBytes.Length == 0)
        {
            _fieldState.SetThumbnailSource("Thumbnail", null);
            _fieldState.SetLoading("Thumbnail", false);
            return;
        }

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter())
            {
                writer.WriteBytes(thumbnailBytes);
                var buffer = writer.DetachBuffer();
                await stream.WriteAsync(buffer);
            }

            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);

            if (!_isDisposed() && _hasCurrentSelection() && selectionVersion == _currentSelectionVersion())
            {
                _fieldState.SetThumbnailSource("Thumbnail", bitmap);
                _fieldState.SetValue("Thumbnail", "Preview");
                _fieldState.SetLoading("Thumbnail", false);
                _refreshVisibleCategories();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to materialize thumbnail preview.");
            if (!_isDisposed() && _hasCurrentSelection() && selectionVersion == _currentSelectionVersion())
            {
                _fieldState.SetThumbnailSource("Thumbnail", null);
                _fieldState.SetLoading("Thumbnail", false);
            }
        }
    }
}
