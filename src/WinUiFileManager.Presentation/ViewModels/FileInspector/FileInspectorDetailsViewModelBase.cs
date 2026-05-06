using System.Runtime.CompilerServices;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

public abstract partial class FileInspectorDetailsViewModelBase : ObservableObject, IDisposable
{
    private static readonly TimeSpan DeferredLoadTimeout = TimeSpan.FromSeconds(5);

    private readonly IFileIdentityService _fileIdentityService;
    private readonly IClipboardService _clipboardService;
    private readonly IShellService _shellService;
    private readonly ILogger<FileInspectorViewModel> _logger;
    private readonly FileInspectorFieldState _fieldState;
    private readonly FileInspectorDeferredLoader _deferredLoader;
    private readonly List<InspectorBatchDefinition> _deferredBatches;
    private long _currentSelectionVersion;
    private string _currentFullPath = string.Empty;
    private long _tableSelectionRefreshVersion;
    private IReadOnlyList<SpecFileEntryViewModel> _lastTableSelection = [];
    private FileInspectorSelection? _currentTableSelection;
    private bool _preserveDeferredVisibilityUntilFinalBatch;
    private bool _disposed;

    [ObservableProperty]
    public partial bool HasItem { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingDetails { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public ObservableCollection<FileInspectorFieldViewModel> Fields { get; }

    public ObservableCollection<FileInspectorCategoryViewModel> Categories { get; }

    protected FileInspectorDetailsViewModelBase(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        IShellService shellService,
        ISchedulerProvider schedulers,
        ILogger<FileInspectorViewModel> logger)
    {
        _fileIdentityService = fileIdentityService;
        _clipboardService = clipboardService;
        _shellService = shellService;
        _logger = logger;

        var inspectorModel = new FileInspectorModelBuilder(
            NtfsFileInspectorCategory.CanToggleField,
            ToggleNtfsFlagAsync).Build();
        _fieldState = new FileInspectorFieldState(inspectorModel);
        Fields = _fieldState.Fields;
        Categories = _fieldState.Categories;

        _deferredBatches =
        [
            new InspectorBatchDefinition(
                FileInspectorCategory.Ids,
                IsFinalBatch: false,
                (selection, token) => IdentityFileInspectorCategory.LoadAsync(
                    _fileIdentityService,
                    _logger,
                    selection,
                    DeferredLoadTimeout,
                    token)),
            new InspectorBatchDefinition(
                FileInspectorCategory.Locks,
                IsFinalBatch: false,
                (selection, token) => LocksFileInspectorCategory.LoadAsync(
                    _fileIdentityService,
                    _logger,
                    selection,
                    DeferredLoadTimeout,
                    token)),
            new InspectorBatchDefinition(
                FileInspectorCategory.Links,
                IsFinalBatch: false,
                (selection, token) => LinksFileInspectorCategory.LoadAsync(
                    _fileIdentityService,
                    _logger,
                    selection,
                    DeferredLoadTimeout,
                    token)),
            new InspectorBatchDefinition(
                FileInspectorCategory.Streams,
                IsFinalBatch: false,
                (selection, token) => StreamsFileInspectorCategory.LoadAsync(
                    _fileIdentityService,
                    _logger,
                    selection,
                    DeferredLoadTimeout,
                    token)),
            new InspectorBatchDefinition(
                FileInspectorCategory.Security,
                IsFinalBatch: false,
                (selection, token) => SecurityFileInspectorCategory.LoadAsync(
                    _fileIdentityService,
                    _logger,
                    selection,
                    DeferredLoadTimeout,
                    token)),
            new InspectorBatchDefinition(
                FileInspectorCategory.Thumbnails,
                IsFinalBatch: false,
                (selection, token) => ThumbnailsFileInspectorCategory.LoadAsync(
                    _fileIdentityService,
                    _logger,
                    selection,
                    DeferredLoadTimeout,
                    token)),
            new InspectorBatchDefinition(
                FileInspectorCategory.Cloud,
                IsFinalBatch: true,
                (selection, token) => CloudFileInspectorCategory.LoadAsync(
                    _fileIdentityService,
                    _logger,
                    selection,
                    DeferredLoadTimeout,
                    token))
        ];

        _deferredLoader = new FileInspectorDeferredLoader(
            schedulers,
            logger,
            LoadDeferredBatchesAsync,
            ApplyDeferredBatch,
            () => _disposed);

    }

    public void ApplySelection(FileInspectorSelection selection)
    {
        if (_disposed)
        {
            return;
        }

        var hadSelection = !string.IsNullOrWhiteSpace(_currentFullPath);
        HasItem = selection.HasItem;
        if (!selection.HasItem)
        {
            Clear();
            return;
        }

        var isSameItem = hadSelection
            && string.Equals(_currentFullPath, selection.FullPath, StringComparison.OrdinalIgnoreCase);
        var isSameVersion = selection.RefreshVersion == _currentSelectionVersion;

        if (isSameItem && isSameVersion)
        {
            IsLoadingDetails = selection.CanLoadDeferred;
            return;
        }

        var preserveDeferredVisibility = hadSelection;
        ApplyBasicSelection(selection, preserveDeferredVisibility);
        _currentSelectionVersion = selection.RefreshVersion;
        IsLoadingDetails = selection.CanLoadDeferred;
    }

    [RelayCommand]
    private async Task CopyAllAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentFullPath))
        {
            return;
        }

        var builder = new StringBuilder();
        foreach (var grouping in Categories
            .Where(static category => category.HasVisibleFields)
            .OrderBy(static category => FileInspectorCategorySort.GetSortOrder(category.Category)))
        {
            builder.AppendLine(grouping.Name);
            foreach (var field in Fields
                .Where(field => field.IsVisible && field.Category == grouping.Category)
                .OrderBy(static field => field.SortOrder))
            {
                builder.Append("  ").Append(field.Key).Append(": ").AppendLine(field.Value);
            }

            builder.AppendLine();
        }

        await _clipboardService.SetTextAsync(builder.ToString().TrimEnd(), CancellationToken.None);
    }

    [RelayCommand]
    private void Refresh()
    {
        if (_lastTableSelection.Count == 1)
        {
            ApplyTableSelection(_lastTableSelection);
        }
    }

    [RelayCommand]
    private async Task ShowPropertiesAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentFullPath))
        {
            return;
        }

        await _shellService.ShowPropertiesAsync(
            NormalizedPath.FromUserInput(_currentFullPath),
            CancellationToken.None);
    }

    public void Clear()
    {
        IsLoadingDetails = false;
        _currentFullPath = string.Empty;
        _preserveDeferredVisibilityUntilFinalBatch = false;
        _fieldState.ClearValues();
        RefreshVisibleCategories();
    }

    public virtual void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _deferredLoader.Dispose();
        GC.SuppressFinalize(this);
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshVisibleCategories();
    }

    protected void ApplyTableSelection(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        ApplyBasicTableSelection(selectedEntries);
        LoadDeferredTableSelection(selectedEntries);
    }

    internal void ApplyBasicTableSelection(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        _lastTableSelection = selectedEntries;
        _ = Interlocked.Increment(ref _tableSelectionRefreshVersion);

        var selection = FileInspectorSelection.FromSelection(
            selectedEntries,
            _tableSelectionRefreshVersion);

        _currentTableSelection = selection;
        ApplySelection(selection);
    }

    internal void LoadDeferredTableSelection(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        if (selectedEntries.Count != 1)
        {
            return;
        }

        var selection = _currentTableSelection?.RefreshVersion == _currentSelectionVersion
            ? _currentTableSelection
            : FileInspectorSelection.FromSelection(selectedEntries, _currentSelectionVersion);
        StartDeferredLoad(selection);
    }

    internal void ShowNoSelection()
    {
        _lastTableSelection = [];
        _currentTableSelection = null;
        _deferredLoader.Cancel();
        var refreshVersion = Interlocked.Increment(ref _tableSelectionRefreshVersion);
        ApplySelection(FileInspectorSelection.NoSelection(refreshVersion));
    }

    private void StartDeferredLoad(FileInspectorSelection selection)
    {
        _deferredLoader.Start(selection);
    }

    private void ApplyBasicSelection(FileInspectorSelection selection, bool preserveDeferredVisibility)
    {
        BasicFileInspectorCategory.ApplySelection(selection, _fieldState);
        _currentFullPath = selection.FullPath;

        if (preserveDeferredVisibility)
        {
            _fieldState.BeginDeferredRefresh();
        }
        else
        {
            NtfsFileInspectorCategory.ApplyAttributes(selection.AttributesFlags, _fieldState);
            _fieldState.ClearDeferredFields();
        }

        _preserveDeferredVisibilityUntilFinalBatch = preserveDeferredVisibility;
        RefreshVisibleCategories(preserveDeferredVisibility);
    }

    private void SetFieldValue(string key, string value)
    {
        _fieldState.SetValue(key, value);
    }

    private void SetFieldThumbnailSource(string key, ImageSource? value)
    {
        _fieldState.SetThumbnailSource(key, value);
    }

    private void SetFieldLoading(string key, bool isLoading)
    {
        _fieldState.SetLoading(key, isLoading);
    }

    private async Task<bool> ToggleNtfsFlagAsync(string key, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(_currentFullPath)
            || !NtfsFileInspectorCategory.TryGetToggleFlag(key, out var flag))
        {
            return false;
        }

        try
        {
            var updated = await _fileIdentityService.SetNtfsAttributeFlagAsync(
                _currentFullPath,
                flag,
                enabled,
                CancellationToken.None);

            if (updated)
            {
                ApplyTableSelection(_lastTableSelection);
            }

            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to toggle NTFS flag {Flag} for {Path}", key, _currentFullPath);
            return false;
        }
    }

    private void RefreshVisibleCategories(bool preserveDeferredVisibility = false)
    {
        if (_disposed)
        {
            return;
        }

        _fieldState.RefreshVisibleCategories(
            _currentFullPath,
            SearchText,
            preserveDeferredVisibility);
        OnPropertyChanged(nameof(HasVisibleFields));
    }

    public bool HasVisibleFields => _fieldState.HasVisibleFields;

    public async IAsyncEnumerable<FileInspectorDeferredBatchResult> LoadDeferredBatchesAsync(
        FileInspectorSelection selection,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_disposed || !selection.CanLoadDeferred)
        {
            yield break;
        }

        foreach (var batch in _deferredBatches)
        {
            var loadResult = await batch.LoadAsync(selection, cancellationToken);
            yield return new FileInspectorDeferredBatchResult(
                selection.RefreshVersion,
                batch.Category,
                batch.IsFinalBatch,
                loadResult.Updates,
                loadResult.ThumbnailBytes);
        }
    }

    public void ApplyDeferredBatch(FileInspectorDeferredBatchResult batchResult)
    {
        if (_disposed || !_hasCurrentSelection)
        {
            return;
        }

        if (batchResult.SelectionVersion != _currentSelectionVersion)
        {
            return;
        }

        foreach (var update in batchResult.Updates)
        {
            SetFieldValue(update.Key, update.Value);
            SetFieldLoading(update.Key, false);
        }

        if (batchResult.Category == FileInspectorCategory.Thumbnails)
        {
            _ = ApplyThumbnailSourceAsync(batchResult.SelectionVersion, batchResult.ThumbnailBytes);
        }

        if (batchResult.IsFinalBatch)
        {
            _preserveDeferredVisibilityUntilFinalBatch = false;
            RefreshVisibleCategories();
        }
        else
        {
            RefreshVisibleCategories(_preserveDeferredVisibilityUntilFinalBatch);
        }

        if (batchResult.IsFinalBatch)
        {
            IsLoadingDetails = false;
            if (string.IsNullOrWhiteSpace(_currentFullPath))
            {
                return;
            }
        }
    }

    private bool _hasCurrentSelection => !string.IsNullOrWhiteSpace(_currentFullPath);

    private async Task ApplyThumbnailSourceAsync(long selectionVersion, byte[]? thumbnailBytes)
    {
        if (_disposed || !_hasCurrentSelection || selectionVersion != _currentSelectionVersion)
        {
            return;
        }

        if (thumbnailBytes is null || thumbnailBytes.Length == 0)
        {
            SetFieldThumbnailSource("Thumbnail", null);
            SetFieldLoading("Thumbnail", false);
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

            if (!_disposed && _hasCurrentSelection && selectionVersion == _currentSelectionVersion)
            {
                SetFieldThumbnailSource("Thumbnail", bitmap);
                SetFieldValue("Thumbnail", "Preview");
                SetFieldLoading("Thumbnail", false);
                RefreshVisibleCategories();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to materialize thumbnail preview.");
            if (!_disposed && _hasCurrentSelection && selectionVersion == _currentSelectionVersion)
            {
                SetFieldThumbnailSource("Thumbnail", null);
                SetFieldLoading("Thumbnail", false);
            }
        }
    }

    private sealed record InspectorBatchDefinition(
        FileInspectorCategory Category,
        bool IsFinalBatch,
        Func<FileInspectorSelection, CancellationToken, Task<FileInspectorBatchLoadResult>> LoadAsync);
}
