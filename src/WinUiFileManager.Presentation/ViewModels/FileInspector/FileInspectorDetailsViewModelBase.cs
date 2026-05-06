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
                LoadIdentityBatchAsync),
            new InspectorBatchDefinition(
                FileInspectorCategory.Locks,
                IsFinalBatch: false,
                LoadLockDiagnosticsBatchAsync),
            new InspectorBatchDefinition(
                FileInspectorCategory.Links,
                IsFinalBatch: false,
                LoadLinkBatchAsync),
            new InspectorBatchDefinition(
                FileInspectorCategory.Streams,
                IsFinalBatch: false,
                LoadStreamBatchAsync),
            new InspectorBatchDefinition(
                FileInspectorCategory.Security,
                IsFinalBatch: false,
                LoadSecurityBatchAsync),
            new InspectorBatchDefinition(
                FileInspectorCategory.Thumbnails,
                IsFinalBatch: false,
                LoadThumbnailBatchAsync),
            new InspectorBatchDefinition(
                FileInspectorCategory.Cloud,
                IsFinalBatch: true,
                LoadCloudBatchAsync)
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

    private async Task<InspectorBatchLoadResult> LoadNtfsBatchAsync(
        FileInspectorSelection selection,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DeferredLoadTimeout);

            var details = await _fileIdentityService.GetNtfsMetadataDetailsAsync(selection.FullPath, timeoutCts.Token);
            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Read Only", FormatFlag(details.Attributes.HasFlag(FileAttributes.ReadOnly))),
                new FileInspectorFieldUpdate("Hidden", FormatFlag(details.Attributes.HasFlag(FileAttributes.Hidden))),
                new FileInspectorFieldUpdate("System", FormatFlag(details.Attributes.HasFlag(FileAttributes.System))),
                new FileInspectorFieldUpdate("Archive", FormatFlag(details.Attributes.HasFlag(FileAttributes.Archive))),
                new FileInspectorFieldUpdate("Temporary", FormatFlag(details.Attributes.HasFlag(FileAttributes.Temporary))),
                new FileInspectorFieldUpdate("Offline", FormatFlag(details.Attributes.HasFlag(FileAttributes.Offline))),
                new FileInspectorFieldUpdate("Not Content Indexed", FormatFlag(details.Attributes.HasFlag(FileAttributes.NotContentIndexed))),
                new FileInspectorFieldUpdate("Encrypted", FormatFlag(details.Attributes.HasFlag(FileAttributes.Encrypted))),
                new FileInspectorFieldUpdate("Compressed", FormatFlag(details.Attributes.HasFlag(FileAttributes.Compressed))),
                new FileInspectorFieldUpdate("Sparse", FormatFlag(details.Attributes.HasFlag(FileAttributes.SparseFile))),
                new FileInspectorFieldUpdate("Reparse Point", FormatFlag(details.Attributes.HasFlag(FileAttributes.ReparsePoint))),
                new FileInspectorFieldUpdate("Created", FormatRequiredUtc(details.CreationTimeUtc)),
                new FileInspectorFieldUpdate("Accessed", FormatRequiredUtc(details.LastAccessTimeUtc)),
                new FileInspectorFieldUpdate("Modified", FormatRequiredUtc(details.LastWriteTimeUtc)),
                new FileInspectorFieldUpdate("MFT Changed", FormatRequiredUtc(details.ChangeTimeUtc))
            ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load NTFS metadata for {Path}", selection.FullPath);
            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Created", "Unavailable"),
                new FileInspectorFieldUpdate("Accessed", "Unavailable"),
                new FileInspectorFieldUpdate("Modified", "Unavailable"),
                new FileInspectorFieldUpdate("MFT Changed", "Unavailable")
            ]);
        }
    }

    private async Task<InspectorBatchLoadResult> LoadIdentityBatchAsync(
        FileInspectorSelection selection,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DeferredLoadTimeout);

            var ntfsDetails = await _fileIdentityService.GetNtfsMetadataDetailsAsync(selection.FullPath, timeoutCts.Token);
            var details = await _fileIdentityService.GetIdentityDetailsAsync(selection.FullPath, timeoutCts.Token);
            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Created", FormatRequiredUtc(ntfsDetails.CreationTimeUtc)),
                new FileInspectorFieldUpdate("Accessed", FormatRequiredUtc(ntfsDetails.LastAccessTimeUtc)),
                new FileInspectorFieldUpdate("Modified", FormatRequiredUtc(ntfsDetails.LastWriteTimeUtc)),
                new FileInspectorFieldUpdate("MFT Changed", FormatRequiredUtc(ntfsDetails.ChangeTimeUtc)),
                new FileInspectorFieldUpdate(
                    "File ID",
                    details.FileId == NtfsFileId.None ? "Unavailable" : details.FileId.HexDisplay),
                new FileInspectorFieldUpdate("Volume Serial", details.VolumeSerial),
                new FileInspectorFieldUpdate("File Index (64-bit)", details.LegacyFileIndex),
                new FileInspectorFieldUpdate("Hard Link Count", details.HardLinkCount),
                new FileInspectorFieldUpdate("Final Path", details.FinalPath)
            ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load identity details for {Path}", selection.FullPath);
            return new InspectorBatchLoadResult([new FileInspectorFieldUpdate("File ID", "Unavailable")]);
        }
    }

    private async Task<InspectorBatchLoadResult> LoadLinkBatchAsync(
        FileInspectorSelection selection,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DeferredLoadTimeout);

            var diagnostics = await _fileIdentityService.GetLinkDiagnosticsAsync(selection.FullPath, timeoutCts.Token);
            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Link Target", diagnostics.LinkTarget),
                new FileInspectorFieldUpdate("Link Status", diagnostics.LinkStatus),
                new FileInspectorFieldUpdate("Reparse Tag", diagnostics.ReparseTag),
                new FileInspectorFieldUpdate("Reparse Data", diagnostics.ReparseData),
                new FileInspectorFieldUpdate("Object ID", diagnostics.ObjectId)
            ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load link diagnostics for {Path}", selection.FullPath);
            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Link Target", string.Empty),
                new FileInspectorFieldUpdate("Link Status", string.Empty),
                new FileInspectorFieldUpdate("Reparse Tag", string.Empty),
                new FileInspectorFieldUpdate("Reparse Data", string.Empty),
                new FileInspectorFieldUpdate("Object ID", string.Empty)
            ]);
        }
    }

    private async Task<InspectorBatchLoadResult> LoadStreamBatchAsync(
        FileInspectorSelection selection,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DeferredLoadTimeout);

            var diagnostics = await _fileIdentityService.GetStreamDiagnosticsAsync(selection.FullPath, timeoutCts.Token);
            var hasStreams = !string.IsNullOrWhiteSpace(diagnostics.AlternateStreamCount)
                && diagnostics.AlternateStreamCount != "0";

            return hasStreams
                ? new InspectorBatchLoadResult([
                    new FileInspectorFieldUpdate("Alternate Stream Count", diagnostics.AlternateStreamCount),
                    new FileInspectorFieldUpdate(
                        "Alternate Streams",
                        diagnostics.AlternateStreams.Count == 0
                            ? string.Empty
                            : string.Join(Environment.NewLine, diagnostics.AlternateStreams))
                ])
                : new InspectorBatchLoadResult([
                    new FileInspectorFieldUpdate("Alternate Stream Count", string.Empty),
                    new FileInspectorFieldUpdate("Alternate Streams", string.Empty)
                ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load stream diagnostics for {Path}", selection.FullPath);
            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Alternate Stream Count", string.Empty),
                new FileInspectorFieldUpdate("Alternate Streams", string.Empty)
            ]);
        }
    }

    private async Task<InspectorBatchLoadResult> LoadSecurityBatchAsync(
        FileInspectorSelection selection,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DeferredLoadTimeout);

            var diagnostics = await _fileIdentityService.GetSecurityDiagnosticsAsync(selection.FullPath, timeoutCts.Token);
            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Owner", diagnostics.Owner),
                new FileInspectorFieldUpdate("Group", diagnostics.Group),
                new FileInspectorFieldUpdate("DACL Summary", diagnostics.DaclSummary),
                new FileInspectorFieldUpdate("SACL Summary", diagnostics.SaclSummary),
                new FileInspectorFieldUpdate("Inherited", FormatOptionalBoolean(diagnostics.Inherited)),
                new FileInspectorFieldUpdate("Protected", FormatOptionalBoolean(diagnostics.Protected))
            ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load security diagnostics for {Path}", selection.FullPath);
            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Owner", string.Empty),
                new FileInspectorFieldUpdate("Group", string.Empty),
                new FileInspectorFieldUpdate("DACL Summary", string.Empty),
                new FileInspectorFieldUpdate("SACL Summary", string.Empty),
                new FileInspectorFieldUpdate("Inherited", string.Empty),
                new FileInspectorFieldUpdate("Protected", string.Empty)
            ]);
        }
    }

    private async Task<InspectorBatchLoadResult> LoadThumbnailBatchAsync(
        FileInspectorSelection selection,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DeferredLoadTimeout);

            var diagnostics = await _fileIdentityService.GetThumbnailDiagnosticsAsync(selection.FullPath, timeoutCts.Token);
            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Has Thumbnail", diagnostics.ThumbnailBytes is { Length: > 0 } ? "Yes" : "No"),
                new FileInspectorFieldUpdate("Association", diagnostics.ProgId)
            ], diagnostics.ThumbnailBytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load thumbnail diagnostics for {Path}", selection.FullPath);
            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Has Thumbnail", "No"),
                new FileInspectorFieldUpdate("Association", string.Empty)
            ]);
        }
    }

    private async Task<InspectorBatchLoadResult> LoadCloudBatchAsync(
        FileInspectorSelection selection,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DeferredLoadTimeout);

            var diagnostics = await _fileIdentityService.GetCloudDiagnosticsAsync(selection.FullPath, timeoutCts.Token);
            if (!diagnostics.IsCloudControlled)
            {
                return new InspectorBatchLoadResult(
                [
                    new FileInspectorFieldUpdate("Status", string.Empty),
                    new FileInspectorFieldUpdate("Provider", string.Empty),
                    new FileInspectorFieldUpdate("Sync Root", string.Empty),
                    new FileInspectorFieldUpdate("Root ID", string.Empty),
                    new FileInspectorFieldUpdate("Provider ID", string.Empty),
                    new FileInspectorFieldUpdate("Available", string.Empty),
                    new FileInspectorFieldUpdate("Transfer", string.Empty),
                    new FileInspectorFieldUpdate("Custom", string.Empty)
                ]);
            }

            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Status", diagnostics.Status),
                new FileInspectorFieldUpdate("Provider", diagnostics.Provider),
                new FileInspectorFieldUpdate("Sync Root", diagnostics.SyncRoot),
                new FileInspectorFieldUpdate("Root ID", diagnostics.SyncRootId),
                new FileInspectorFieldUpdate("Provider ID", diagnostics.ProviderId),
                new FileInspectorFieldUpdate("Available", diagnostics.Available),
                new FileInspectorFieldUpdate("Transfer", diagnostics.Transfer),
                new FileInspectorFieldUpdate("Custom", diagnostics.Custom)
            ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load cloud diagnostics for {Path}", selection.FullPath);
            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Status", string.Empty),
                new FileInspectorFieldUpdate("Provider", string.Empty),
                new FileInspectorFieldUpdate("Sync Root", string.Empty),
                new FileInspectorFieldUpdate("Root ID", string.Empty),
                new FileInspectorFieldUpdate("Provider ID", string.Empty),
                new FileInspectorFieldUpdate("Available", string.Empty),
                new FileInspectorFieldUpdate("Transfer", string.Empty),
                new FileInspectorFieldUpdate("Custom", string.Empty)
            ]);
        }
    }

    private async Task<InspectorBatchLoadResult> LoadLockDiagnosticsBatchAsync(
        FileInspectorSelection selection,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DeferredLoadTimeout);

            var diagnostics = await _fileIdentityService.GetLockDiagnosticsAsync(selection.FullPath, timeoutCts.Token);
            if (!HasPositiveLockEvidence(diagnostics))
            {
                return new InspectorBatchLoadResult(
                [
                    new FileInspectorFieldUpdate("Is locked", "False"),
                    new FileInspectorFieldUpdate("In Use", string.Empty),
                    new FileInspectorFieldUpdate("Locked By", string.Empty),
                    new FileInspectorFieldUpdate("Lock PIDs", string.Empty),
                    new FileInspectorFieldUpdate("Lock Services", string.Empty)
                ]);
            }

            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Is locked", "True"),
                new FileInspectorFieldUpdate("In Use", FormatOptionalBoolean(diagnostics.InUse)),
                new FileInspectorFieldUpdate(
                    "Locked By",
                    diagnostics.LockBy.Count == 0 ? string.Empty : string.Join(Environment.NewLine, diagnostics.LockBy)),
                new FileInspectorFieldUpdate(
                    "Lock PIDs",
                    diagnostics.LockPids.Count == 0 ? string.Empty : string.Join(", ", diagnostics.LockPids)),
                new FileInspectorFieldUpdate(
                    "Lock Services",
                    diagnostics.LockServices.Count == 0 ? string.Empty : string.Join(", ", diagnostics.LockServices))
            ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load lock diagnostics for {Path}", selection.FullPath);
            return new InspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Is locked", "False"),
                new FileInspectorFieldUpdate("In Use", string.Empty),
                new FileInspectorFieldUpdate("Locked By", string.Empty),
                new FileInspectorFieldUpdate("Lock PIDs", string.Empty),
                new FileInspectorFieldUpdate("Lock Services", string.Empty)
            ]);
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

    private static bool HasPositiveLockEvidence(FileLockDiagnostics diagnostics) =>
        diagnostics.InUse == true
        || diagnostics.LockBy.Count > 0
        || diagnostics.LockPids.Count > 0
        || diagnostics.LockServices.Count > 0;

    private static string FormatUtc(DateTime value) =>
        value == DateTime.MinValue
            ? string.Empty
            : value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

    private static string FormatRequiredUtc(DateTime value) =>
        value == DateTime.MinValue
            ? "Unavailable"
            : value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

    private static string FormatOptionalBoolean(bool? value) =>
        value switch
        {
            true => "Yes",
            false => "No",
            _ => string.Empty
        };

    private static string FormatFlag(bool value) => value ? "Yes" : "No";

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
        Func<FileInspectorSelection, CancellationToken, Task<InspectorBatchLoadResult>> LoadAsync);

    private sealed record InspectorBatchLoadResult(
        IReadOnlyList<FileInspectorFieldUpdate> Updates,
        byte[]? ThumbnailBytes = null);
}
