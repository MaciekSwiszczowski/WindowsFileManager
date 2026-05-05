using System.Collections.Frozen;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;
using Windows.Storage.Streams;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FileInspectorViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan DeferredLoadTimeout = TimeSpan.FromSeconds(5);

    private readonly IFileIdentityService _fileIdentityService;
    private readonly IClipboardService _clipboardService;
    private readonly IShellService _shellService;
    private readonly FileTableFocusService _fileTableFocusService;
    private readonly ISchedulerProvider _schedulers;
    private readonly ILogger<FileInspectorViewModel> _logger;
    private readonly Dictionary<string, FileInspectorFieldViewModel> _fieldMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileInspectorCategoryViewModel> _categoryMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<InspectorBatchDefinition> _deferredBatches;
    private readonly HashSet<string> _deferredFieldKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly FrozenDictionary<string, FileAttributes> ToggleableNtfsFlags =
        new Dictionary<string, FileAttributes>(StringComparer.OrdinalIgnoreCase)
        {
            ["Read Only"] = FileAttributes.ReadOnly,
            ["Hidden"] = FileAttributes.Hidden,
            ["Archive"] = FileAttributes.Archive,
            ["Temporary"] = FileAttributes.Temporary,
            ["Not Content Indexed"] = FileAttributes.NotContentIndexed
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    private long _currentSelectionVersion;
    private string _currentFullPath = string.Empty;
    private long _tableSelectionRefreshVersion;
    private IReadOnlyList<SpecFileEntryViewModel> _lastTableSelection = [];
    private CancellationTokenSource _deferredLoadCancellation = new();
    private bool _preserveDeferredVisibilityUntilFinalBatch;
    private bool _disposed;

    [ObservableProperty]
    public partial bool HasItem { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingDetails { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public ObservableCollection<FileInspectorFieldViewModel> Fields { get; } = [];

    public ObservableCollection<FileInspectorCategoryViewModel> Categories { get; } = [];

    public Visibility DetailsVisibility => HasItem ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SearchVisibility => HasItem && string.IsNullOrWhiteSpace(StatusMessage)
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility StatusMessageVisibility => string.IsNullOrWhiteSpace(StatusMessage)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility EmptyStateVisibility => HasItem ? Visibility.Collapsed : Visibility.Visible;

    public FileInspectorViewModel(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        IShellService shellService,
        FileTableFocusService fileTableFocusService,
        ISchedulerProvider schedulers,
        ILogger<FileInspectorViewModel> logger)
    {
        _fileIdentityService = fileIdentityService;
        _clipboardService = clipboardService;
        _shellService = shellService;
        _fileTableFocusService = fileTableFocusService;
        _schedulers = schedulers;
        _logger = logger;

        InitializeFieldDefinitions();
        _deferredBatches =
        [
            new InspectorBatchDefinition(
                "IDs",
                IsFinalBatch: false,
                LoadIdentityBatchAsync),
            new InspectorBatchDefinition(
                "Locks",
                IsFinalBatch: false,
                LoadLockDiagnosticsBatchAsync),
            new InspectorBatchDefinition(
                "Links",
                IsFinalBatch: false,
                LoadLinkBatchAsync),
            new InspectorBatchDefinition(
                "Streams",
                IsFinalBatch: false,
                LoadStreamBatchAsync),
            new InspectorBatchDefinition(
                "Security",
                IsFinalBatch: false,
                LoadSecurityBatchAsync),
            new InspectorBatchDefinition(
                "Thumbnails",
                IsFinalBatch: false,
                LoadThumbnailBatchAsync),
            new InspectorBatchDefinition(
                "Cloud",
                IsFinalBatch: true,
                LoadCloudBatchAsync)
        ];

        SubscribeToTableMessages();
    }

    public void ApplySelection(FileInspectorSelection selection)
    {
        if (_disposed)
        {
            return;
        }

        if (!selection.HasItem)
        {
            Clear(selection.StatusMessage);
            return;
        }

        var hadItem = HasItem;
        var isSameItem = hadItem
            && string.Equals(_currentFullPath, selection.FullPath, StringComparison.OrdinalIgnoreCase);
        var isSameVersion = selection.RefreshVersion == _currentSelectionVersion;

        if (isSameItem && isSameVersion)
        {
            IsLoadingDetails = selection.CanLoadDeferred;
            StatusMessage = string.Empty;
            return;
        }

        var preserveDeferredVisibility = hadItem;
        ApplyBasicSelection(selection, preserveDeferredVisibility);
        _currentSelectionVersion = selection.RefreshVersion;
        IsLoadingDetails = selection.CanLoadDeferred;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task CopyAllAsync()
    {
        if (!HasItem)
        {
            return;
        }

        var builder = new StringBuilder();
        foreach (var grouping in Categories
            .Where(static category => category.HasVisibleFields)
            .OrderBy(category => GetCategorySortOrder(category.Name)))
        {
            builder.AppendLine(grouping.Name);
            foreach (var field in Fields
                .Where(field => field.IsVisible && string.Equals(field.Category, grouping.Name, StringComparison.OrdinalIgnoreCase))
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
        if (HasItem)
        {
            ApplyTableSelection(_lastTableSelection);
        }
    }

    [RelayCommand]
    private async Task ShowPropertiesAsync()
    {
        if (!HasItem || string.IsNullOrWhiteSpace(_currentFullPath))
        {
            return;
        }

        await _shellService.ShowPropertiesAsync(
            NormalizedPath.FromUserInput(_currentFullPath),
            CancellationToken.None);
    }

    public void Clear(string statusMessage)
    {
        IsLoadingDetails = false;
        HasItem = false;
        StatusMessage = statusMessage;
        _currentFullPath = string.Empty;
        _preserveDeferredVisibilityUntilFinalBatch = false;
        ClearFieldValues();
        RefreshVisibleCategories();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _deferredLoadCancellation.Cancel();
        _deferredLoadCancellation.Dispose();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _fileTableFocusService.PropertyChanged -= OnFileTableFocusServicePropertyChanged;
    }

    partial void OnHasItemChanged(bool value)
    {
        OnPropertyChanged(nameof(DetailsVisibility));
        OnPropertyChanged(nameof(SearchVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(SearchVisibility));
        OnPropertyChanged(nameof(StatusMessageVisibility));
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshVisibleCategories();
    }

    private void InitializeFieldDefinitions()
    {
        RegisterField("Basic", "Name", "File or folder name", 0);
        RegisterField("Basic", "Full Path", "Full selected item path", 1);
        RegisterField("Basic", "Type", "Item type", 2);
        RegisterField("Basic", "Extension", "File extension", 3);
        RegisterField("Basic", "Size", "Size in a human-readable format", 4);
        RegisterField("Basic", "Attributes", "File system attributes", 5);

        RegisterField("NTFS", "Created", "NTFS creation time in UTC.", 0);
        RegisterField("NTFS", "Accessed", "NTFS last access time in UTC.", 1);
        RegisterField("NTFS", "Modified", "NTFS last write time in UTC.", 2);
        RegisterField("NTFS", "MFT Changed", "NTFS metadata change time in UTC.", 3);
        RegisterField("NTFS", "Read Only", "Whether the item is marked read-only.", 4);
        RegisterField("NTFS", "Hidden", "Whether the item is hidden.", 5);
        RegisterField("NTFS", "System", "Whether the item is marked as a system file.", 6);
        RegisterField("NTFS", "Archive", "Whether the archive attribute is set.", 7);
        RegisterField("NTFS", "Temporary", "Whether the item is marked temporary.", 8);
        RegisterField("NTFS", "Offline", "Whether the item is offline or placeholder-backed.", 9);
        RegisterField("NTFS", "Not Content Indexed", "Whether the item should be excluded from content indexing.", 10);
        RegisterField("NTFS", "Encrypted", "Whether the item is encrypted with EFS.", 11);
        RegisterField("NTFS", "Compressed", "Whether the item is compressed by NTFS.", 12);
        RegisterField("NTFS", "Sparse", "Whether the item is stored as a sparse file.", 13);
        RegisterField("NTFS", "Reparse Point", "Whether the item is a reparse point.", 14);

        RegisterField("IDs", "File ID", "128-bit NTFS identifier for the selected file system entry.", 0);
        RegisterField("IDs", "Volume Serial", "Volume serial number of the drive that contains the item.", 1);
        RegisterField("IDs", "File Index (64-bit)", "Older 64-bit file index from the legacy Windows API. Diagnostic/compatibility value only.", 2);
        RegisterField("IDs", "Hard Link Count", "How many hard links point to the same file record, when available.", 3);
        RegisterField("IDs", "Final Path", "The resolved final path reported by Windows.", 4);

        RegisterField("Links", "Link Target", "Target path of a symbolic link, junction, or shell shortcut.", 0);
        RegisterField("Links", "Link Status", "What kind of link Windows reports for the item.", 1);
        RegisterField("Links", "Reparse Tag", "Reparse point classification reported by Windows.", 2);
        RegisterField("Links", "Reparse Data", "Additional reparse data, when Windows can provide it.", 3);
        RegisterField("Links", "Object ID", "NTFS object identifier, when available.", 4);

        RegisterField("Streams", "Alternate Stream Count", "How many alternate data streams the item has.", 0);
        RegisterField("Streams", "Alternate Streams", "Names and sizes of alternate data streams.", 1);

        RegisterField("Security", "Owner", "Owner of the file or folder.", 0);
        RegisterField("Security", "Group", "Primary group of the file or folder.", 1);
        RegisterField("Security", "DACL Summary", "Summary of access rules from the discretionary access control list.", 2);
        RegisterField("Security", "SACL Summary", "Summary of audit rules from the system access control list.", 3);
        RegisterField("Security", "Inherited", "Whether the permissions are inherited.", 4);
        RegisterField("Security", "Protected", "Whether inherited permissions are blocked.", 5);

        RegisterField("Thumbnails", "Thumbnail", "Thumbnail preview reported by Windows, when available.", 0);
        RegisterField("Thumbnails", "Has Thumbnail", "Whether Windows could provide a thumbnail for the selected item.", 1);
        RegisterField("Thumbnails", "Association", "Shell association or file type hint used for the thumbnail, when available.", 2);

        RegisterField("Cloud", "Status", "Combined cloud-file state summary such as hydrated, dehydrated, pinned, synced, or uploading.", 0);
        RegisterField("Cloud", "Provider", "Cloud provider display name.", 1);
        RegisterField("Cloud", "Sync Root", "Owning sync-root path or display name.", 2);
        RegisterField("Cloud", "Root ID", "Sync-root registration identifier.", 3);
        RegisterField("Cloud", "Provider ID", "Provider identifier from the sync-root registration.", 4);
        RegisterField("Cloud", "Available", "Whether the selected item is currently available locally.", 5);
        RegisterField("Cloud", "Transfer", "Current transfer state such as upload, download, or paused, when Windows exposes it.", 6);
        RegisterField("Cloud", "Custom", "Provider-defined custom cloud status text, when available.", 7);

        RegisterField("Locks", "Is locked", "Whether the selected item appears to be locked based on the other lock diagnostics in this category.", 0);
        RegisterField("Locks", "In Use", "Whether Windows currently reports the item as in use. Best-effort diagnostic.", 1);
        RegisterField("Locks", "Locked By", "Applications or services that Windows reports as using this item.", 2);
        RegisterField("Locks", "Lock PIDs", "Process IDs of applications using this item. Useful in Task Manager or Process Explorer.", 3);
        RegisterField("Locks", "Lock Services", "Service names associated with the lock, when available.", 4);

        RefreshVisibleCategories();
    }

    private void SubscribeToTableMessages()
    {
        WeakReferenceMessenger.Default.Register<FileTableSelectionChangedMessage>(this, OnFileTableSelectionChanged);
        _fileTableFocusService.PropertyChanged += OnFileTableFocusServicePropertyChanged;
    }

    private void OnFileTableFocusServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileTableFocusService.ActivePanelIdentity))
        {
            ApplyActiveTableSelection();
        }
    }

    private void OnFileTableSelectionChanged(object recipient, FileTableSelectionChangedMessage message)
    {
        if (!string.Equals(message.Identity, _fileTableFocusService.ActivePanelIdentity, StringComparison.Ordinal))
        {
            return;
        }

        ApplyTableSelection(message.SelectedItems);
    }

    private void ApplyActiveTableSelection()
    {
        var identity = _fileTableFocusService.ActivePanelIdentity;
        if (string.IsNullOrWhiteSpace(identity))
        {
            ApplyTableSelection([]);
            return;
        }

        var request = WeakReferenceMessenger.Default.Send(new FileTableSelectedItemsRequestMessage(identity));
        ApplyTableSelection(request.HasReceivedResponse ? request.Response : []);
    }

    private void ApplyTableSelection(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        _lastTableSelection = selectedEntries;
        _ = Interlocked.Increment(ref _tableSelectionRefreshVersion);

        var selection = FileInspectorSelection.FromSelection(
            selectedEntries,
            _tableSelectionRefreshVersion);

        ApplySelection(selection);
        StartDeferredLoad(selection);
    }

    private void StartDeferredLoad(FileInspectorSelection selection)
    {
        CancelCurrentDeferredLoad();

        if (!selection.CanLoadDeferred)
        {
            return;
        }

        _ = LoadDeferredBatchesForSelectionAsync(
            selection,
            _deferredLoadCancellation.Token);
    }

    private async Task LoadDeferredBatchesForSelectionAsync(
        FileInspectorSelection selection,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(async () =>
            {
                await foreach (var batch in LoadDeferredBatchesAsync(selection, cancellationToken)
                    .ConfigureAwait(false))
                {
                    await RunOnMainThreadAsync(
                        () => ApplyDeferredBatch(batch),
                        cancellationToken).ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _disposed)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inspector deferred batch streaming failed");
        }
    }

    private Task RunOnMainThreadAsync(Action action, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _schedulers.MainThread.Schedule(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                action();
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        return completion.Task;
    }

    private void CancelCurrentDeferredLoad()
    {
        _deferredLoadCancellation.Cancel();
        _deferredLoadCancellation.Dispose();
        _deferredLoadCancellation = new CancellationTokenSource();
    }

    private void RegisterField(string category, string key, string tooltip, int sortOrder)
    {
        var field = new FileInspectorFieldViewModel(category, key, tooltip, string.Empty, sortOrder);
        if (ToggleableNtfsFlags.ContainsKey(key))
        {
            field.ConfigureToggle(enabled => ToggleNtfsFlagAsync(key, enabled));
        }

        _fieldMap.Add(key, field);
        Fields.Add(field);
        GetOrCreateCategory(category).Fields.Add(field);
        if (!string.Equals(category, "Basic", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(category, "NTFS", StringComparison.OrdinalIgnoreCase))
        {
            _deferredFieldKeys.Add(key);
        }
    }

    private FileInspectorCategoryViewModel GetOrCreateCategory(string category)
    {
        if (_categoryMap.TryGetValue(category, out var existingCategory))
        {
            return existingCategory;
        }

        var createdCategory = new FileInspectorCategoryViewModel(category);
        _categoryMap.Add(category, createdCategory);
        var insertIndex = 0;
        while (insertIndex < Categories.Count
               && GetCategorySortOrder(Categories[insertIndex].Name) <= GetCategorySortOrder(category))
        {
            insertIndex++;
        }

        Categories.Insert(insertIndex, createdCategory);
        return createdCategory;
    }

    private void ApplyBasicSelection(FileInspectorSelection selection, bool preserveDeferredVisibility)
    {
        SetFieldValue("Name", selection.Name);
        SetFieldValue("Full Path", selection.FullPath);
        SetFieldValue("Type", selection.Kind == ItemKind.Directory ? "Folder" : "File");
        SetFieldValue("Extension", selection.Extension);
        SetFieldValue("Size", FormatSize(selection.SizeBytes));
        SetFieldValue("Attributes", selection.Attributes);
        _currentFullPath = selection.FullPath;

        if (preserveDeferredVisibility)
        {
            BeginDeferredRefresh();
        }
        else
        {
            ApplyNtfsFlags(selection.AttributesFlags);
            ClearDeferredFields();
        }

        HasItem = true;
        _preserveDeferredVisibilityUntilFinalBatch = preserveDeferredVisibility;
        RefreshVisibleCategories(preserveDeferredVisibility);
    }

    private void ClearDeferredFields()
    {
        foreach (var key in _deferredFieldKeys)
        {
            SetFieldValue(key, string.Empty);
            SetFieldLoading(key, false);
            if (string.Equals(key, "Thumbnail", StringComparison.OrdinalIgnoreCase))
            {
                SetFieldThumbnailSource(key, null);
            }
        }
    }

    private void ApplyNtfsFlags(FileAttributes attributes)
    {
        SetFieldValue("Read Only", FormatFlag(attributes.HasFlag(FileAttributes.ReadOnly)));
        SetFieldValue("Hidden", FormatFlag(attributes.HasFlag(FileAttributes.Hidden)));
        SetFieldValue("System", FormatFlag(attributes.HasFlag(FileAttributes.System)));
        SetFieldValue("Archive", FormatFlag(attributes.HasFlag(FileAttributes.Archive)));
        SetFieldValue("Temporary", FormatFlag(attributes.HasFlag(FileAttributes.Temporary)));
        SetFieldValue("Offline", FormatFlag(attributes.HasFlag(FileAttributes.Offline)));
        SetFieldValue("Not Content Indexed", FormatFlag(attributes.HasFlag(FileAttributes.NotContentIndexed)));
        SetFieldValue("Encrypted", FormatFlag(attributes.HasFlag(FileAttributes.Encrypted)));
        SetFieldValue("Compressed", FormatFlag(attributes.HasFlag(FileAttributes.Compressed)));
        SetFieldValue("Sparse", FormatFlag(attributes.HasFlag(FileAttributes.SparseFile)));
        SetFieldValue("Reparse Point", FormatFlag(attributes.HasFlag(FileAttributes.ReparsePoint)));
    }

    private void ClearLockFields()
    {
        SetFieldValue("Is locked", string.Empty);
        SetFieldValue("In Use", string.Empty);
        SetFieldValue("Locked By", string.Empty);
        SetFieldValue("Lock PIDs", string.Empty);
        SetFieldValue("Lock Services", string.Empty);
    }

    private void SetFieldValue(string key, string value)
    {
        if (_fieldMap.TryGetValue(key, out var field))
        {
            field.Value = value;
        }
    }

    private void SetFieldThumbnailSource(string key, ImageSource? value)
    {
        if (_fieldMap.TryGetValue(key, out var field))
        {
            field.ThumbnailSource = value;
        }
    }

    private void SetFieldLoading(string key, bool isLoading)
    {
        if (_fieldMap.TryGetValue(key, out var field))
        {
            field.IsLoading = isLoading;
        }
    }

    private async Task<bool> ToggleNtfsFlagAsync(string key, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(_currentFullPath)
            || !ToggleableNtfsFlags.TryGetValue(key, out var flag))
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

    private void ClearFieldValues()
    {
        foreach (var field in Fields)
        {
            field.Value = string.Empty;
            field.ThumbnailSource = null;
            field.IsLoading = false;
            field.IsVisible = false;
        }
    }

    private void RefreshVisibleCategories(bool preserveDeferredVisibility = false)
    {
        if (_disposed)
        {
            return;
        }

        if (!HasItem)
        {
            foreach (var category in Categories)
            {
                category.RefreshVisibility();
            }

            OnPropertyChanged(nameof(HasVisibleFields));
            return;
        }

        var search = SearchText.Trim();
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        foreach (var field in Fields)
        {
            if (preserveDeferredVisibility
                && IsDeferredField(field)
                && field.IsVisible)
            {
                continue;
            }

            field.IsVisible = ShouldFieldBeVisible(field, search, hasSearch);
        }

        foreach (var category in Categories.OrderBy(category => GetCategorySortOrder(category.Name)))
        {
            category.RefreshVisibility();
        }

        OnPropertyChanged(nameof(HasVisibleFields));
    }

    public bool HasVisibleFields => Categories.Any(static category => category.HasVisibleFields);

    private int GetCategorySortOrder(string category) => category switch
    {
        "Basic" => 0,
        "NTFS" => 1,
        "IDs" => 2,
        "Locks" => 3,
        "Links" => 4,
        "Streams" => 5,
        "Security" => 6,
        "Thumbnails" => 7,
        "Cloud" => 8,
        _ => int.MaxValue
    };

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

        if (batchResult.Category == "Thumbnails")
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
            if (HasItem)
            {
                StatusMessage = string.Empty;
            }
        }
    }

    private bool _hasCurrentSelection => HasItem;

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

    private static string FormatSize(long sizeBytes)
    {
        if (sizeBytes < 0)
        {
            return string.Empty;
        }

        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        var suffixIndex = 0;
        var size = (double)sizeBytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return suffixIndex == 0
            ? $"{size:F0} {suffixes[suffixIndex]}"
            : $"{size:F2} {suffixes[suffixIndex]}";
    }

    private static string FormatOptionalBoolean(bool? value) =>
        value switch
        {
            true => "Yes",
            false => "No",
            _ => string.Empty
        };

    private static string FormatFlag(bool value) => value ? "Yes" : "No";

    private void BeginDeferredRefresh()
    {
        foreach (var key in _deferredFieldKeys)
        {
            if (!_fieldMap.TryGetValue(key, out var field))
            {
                continue;
            }

            if (field.IsVisible)
            {
                field.IsLoading = true;
            }
        }
    }

    private static bool ShouldFieldBeVisible(FileInspectorFieldViewModel field, string search, bool hasSearch)
    {
        if (field.IsLoading)
        {
            return true;
        }

        var hasValue = field.ThumbnailSource is not null || !string.IsNullOrWhiteSpace(field.Value);
        if (!hasValue)
        {
            return false;
        }

        return !hasSearch || field.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDeferredField(FileInspectorFieldViewModel field) =>
        _deferredFieldKeys.Contains(field.Key);

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
        string Category,
        bool IsFinalBatch,
        Func<FileInspectorSelection, CancellationToken, Task<InspectorBatchLoadResult>> LoadAsync);

    private sealed record InspectorBatchLoadResult(
        IReadOnlyList<FileInspectorFieldUpdate> Updates,
        byte[]? ThumbnailBytes = null);
}
