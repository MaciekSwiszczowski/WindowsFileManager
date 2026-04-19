using System.Collections.ObjectModel;
using System.Text;
using System.Linq;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FileInspectorViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan DeferredLoadTimeout = TimeSpan.FromSeconds(5);

    private readonly IFileIdentityService _fileIdentityService;
    private readonly IClipboardService _clipboardService;
    private readonly ILogger<FileInspectorViewModel> _logger;
    private readonly Dictionary<string, FileInspectorFieldViewModel> _fieldMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<InspectorBatchDefinition> _deferredBatches;
    private bool _disposed;

    [ObservableProperty]
    public partial bool HasItem { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingDetails { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public ObservableCollection<FileInspectorCategoryViewModel> Categories { get; } = [];
    public ObservableCollection<FileInspectorFieldViewModel> VisibleFields { get; } = [];

    public Visibility DetailsVisibility => HasItem ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SearchVisibility => HasItem && string.IsNullOrWhiteSpace(StatusMessage)
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility StatusMessageVisibility => string.IsNullOrWhiteSpace(StatusMessage)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility EmptyStateVisibility => HasItem ? Visibility.Collapsed : Visibility.Visible;

    public event EventHandler? RefreshRequested;

    public FileInspectorViewModel(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        ILogger<FileInspectorViewModel> logger)
    {
        _fileIdentityService = fileIdentityService;
        _clipboardService = clipboardService;
        _logger = logger;

        InitializeFieldDefinitions();
        _deferredBatches =
        [
            new InspectorBatchDefinition(
                "Identity",
                IsFinalBatch: false,
                LoadIdentityBatchAsync),
            new InspectorBatchDefinition(
                "Locks",
                IsFinalBatch: true,
                LoadLockDiagnosticsBatchAsync)
        ];
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

        ApplyBasicSelection(selection);
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
        foreach (var grouping in VisibleFields
            .OrderBy(field => GetCategorySortOrder(field.Category))
            .ThenBy(field => field.SortOrder)
            .GroupBy(field => field.Category))
        {
            builder.AppendLine(grouping.Key);
            foreach (var field in grouping)
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
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear(string statusMessage)
    {
        IsLoadingDetails = false;
        HasItem = false;
        StatusMessage = statusMessage;
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
        RegisterField("Basic", "Creation Time (UTC)", "Creation time in UTC", 5);
        RegisterField("Basic", "Last Write Time (UTC)", "Last modified time in UTC", 6);
        RegisterField("Basic", "Attributes", "File system attributes", 7);

        RegisterField("Identity", "NTFS File/Folder ID", "NTFS file identifier", 0);

        RegisterField("Locks", "Is locked", "Whether the selected item appears to be locked based on the other lock diagnostics in this category.", 0);
        RegisterField("Locks", "In Use", "Whether Windows currently reports the item as in use. Best-effort diagnostic.", 1);
        RegisterField("Locks", "Locked By", "Applications or services that Windows reports as using this item.", 2);
        RegisterField("Locks", "Lock PIDs", "Process IDs of applications using this item. Useful in Task Manager or Process Explorer.", 3);
        RegisterField("Locks", "Lock Services", "Service names associated with the lock, when available.", 4);
        RegisterField("Locks", "Usage", "How the owning application is using the item, when Windows can determine it.", 5);
        RegisterField("Locks", "Can Switch To", "Whether Windows reports that the owning application can be brought to the foreground.", 6);
        RegisterField("Locks", "Can Close", "Whether Windows reports that the owning application supports a cooperative close request.", 7);

        RefreshVisibleCategories();
    }

    private void RegisterField(string category, string key, string tooltip, int sortOrder)
    {
        var field = new FileInspectorFieldViewModel(category, key, tooltip, string.Empty, sortOrder);
        _fieldMap.Add(key, field);
    }

    private void ApplyBasicSelection(FileInspectorSelection selection)
    {
        SetFieldValue("Name", selection.Name);
        SetFieldValue("Full Path", selection.FullPath);
        SetFieldValue("Type", selection.Kind == ItemKind.Directory ? "Folder" : "File");
        SetFieldValue("Extension", selection.Extension);
        SetFieldValue("Size", FormatSize(selection.SizeBytes));
        SetFieldValue("Creation Time (UTC)", FormatUtc(selection.CreationTimeUtc));
        SetFieldValue("Last Write Time (UTC)", FormatUtc(selection.LastWriteTimeUtc));
        SetFieldValue("Attributes", selection.Attributes);

        if (selection.CanLoadDeferred)
        {
            ClearDeferredFields();
        }
        else
        {
            ClearDeferredFields();
        }

        HasItem = true;
        RefreshVisibleCategories();
    }

    private void ClearDeferredFields()
    {
        SetFieldValue("NTFS File/Folder ID", string.Empty);
        ClearLockFields();
    }

    private void ClearLockFields()
    {
        SetFieldValue("Is locked", string.Empty);
        SetFieldValue("In Use", string.Empty);
        SetFieldValue("Locked By", string.Empty);
        SetFieldValue("Lock PIDs", string.Empty);
        SetFieldValue("Lock Services", string.Empty);
        SetFieldValue("Usage", string.Empty);
        SetFieldValue("Can Switch To", string.Empty);
        SetFieldValue("Can Close", string.Empty);
    }

    private void SetFieldValue(string key, string value)
    {
        if (_fieldMap.TryGetValue(key, out var field))
        {
            field.Value = value;
        }
    }

    private void ClearFieldValues()
    {
        foreach (var field in _fieldMap.Values)
        {
            field.Value = string.Empty;
        }
    }

    private void RefreshVisibleCategories()
    {
        if (_disposed)
        {
            return;
        }

        if (!HasItem)
        {
            Categories.Clear();
            VisibleFields.Clear();
            OnPropertyChanged(nameof(HasVisibleFields));
            return;
        }

        var search = SearchText.Trim();
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var filteredFields = _fieldMap.Values
            .Where(static field => !string.IsNullOrWhiteSpace(field.Value))
            .Where(field => !hasSearch || field.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(field => GetCategorySortOrder(field.Category))
            .ThenBy(field => field.SortOrder)
            .ToList();

        Categories.Clear();
        VisibleFields.Clear();
        foreach (var grouping in filteredFields.GroupBy(field => field.Category))
        {
            var category = new FileInspectorCategoryViewModel(grouping.Key);
            foreach (var field in grouping.OrderBy(field => field.SortOrder))
            {
                category.Fields.Add(field);
                VisibleFields.Add(field);
            }

            Categories.Add(category);
        }

        OnPropertyChanged(nameof(HasVisibleFields));
    }

    public bool HasVisibleFields => Categories.Count > 0;

    private int GetCategorySortOrder(string category) => category switch
    {
        "Basic" => 0,
        "Identity" => 1,
        "Locks" => 2,
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
            var updates = await batch.LoadAsync(selection, cancellationToken);
            yield return new FileInspectorDeferredBatchResult(
                batch.Category,
                batch.IsFinalBatch,
                updates);
        }
    }

    private async Task<FileInspectorFieldUpdate[]> LoadIdentityBatchAsync(
        FileInspectorSelection selection,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DeferredLoadTimeout);

            var fileId = await _fileIdentityService.GetFileIdAsync(selection.FullPath, timeoutCts.Token);
            return [new FileInspectorFieldUpdate(
                "NTFS File/Folder ID",
                fileId == NtfsFileId.None ? "Unavailable" : fileId.HexDisplay)];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load NTFS file id for {Path}", selection.FullPath);
            return [new FileInspectorFieldUpdate("NTFS File/Folder ID", "Unavailable")];
        }
    }

    private async Task<FileInspectorFieldUpdate[]> LoadLockDiagnosticsBatchAsync(
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
                return
                [
                    new FileInspectorFieldUpdate("Is locked", "False"),
                    new FileInspectorFieldUpdate("In Use", string.Empty),
                    new FileInspectorFieldUpdate("Locked By", string.Empty),
                    new FileInspectorFieldUpdate("Lock PIDs", string.Empty),
                    new FileInspectorFieldUpdate("Lock Services", string.Empty),
                    new FileInspectorFieldUpdate("Usage", string.Empty),
                    new FileInspectorFieldUpdate("Can Switch To", string.Empty),
                    new FileInspectorFieldUpdate("Can Close", string.Empty)
                ];
            }

            return
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
                    diagnostics.LockServices.Count == 0 ? string.Empty : string.Join(", ", diagnostics.LockServices)),
                new FileInspectorFieldUpdate(
                    "Usage",
                    string.IsNullOrWhiteSpace(diagnostics.Usage) ? string.Empty : diagnostics.Usage),
                new FileInspectorFieldUpdate("Can Switch To", FormatOptionalBoolean(diagnostics.CanSwitchTo)),
                new FileInspectorFieldUpdate("Can Close", FormatOptionalBoolean(diagnostics.CanClose))
            ];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load lock diagnostics for {Path}", selection.FullPath);
            return
            [
                new FileInspectorFieldUpdate("Is locked", "False"),
                new FileInspectorFieldUpdate("In Use", string.Empty),
                new FileInspectorFieldUpdate("Locked By", string.Empty),
                new FileInspectorFieldUpdate("Lock PIDs", string.Empty),
                new FileInspectorFieldUpdate("Lock Services", string.Empty),
                new FileInspectorFieldUpdate("Usage", string.Empty),
                new FileInspectorFieldUpdate("Can Switch To", string.Empty),
                new FileInspectorFieldUpdate("Can Close", string.Empty)
            ];
        }
    }

    public void ApplyDeferredBatch(FileInspectorDeferredBatchResult batchResult)
    {
        if (_disposed || !_hasCurrentSelection)
        {
            return;
        }

        foreach (var update in batchResult.Updates)
        {
            SetFieldValue(update.Key, update.Value);
        }

        RefreshVisibleCategories();

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
        || diagnostics.LockServices.Count > 0
        || !string.IsNullOrWhiteSpace(diagnostics.Usage)
        || diagnostics.CanSwitchTo == true
        || diagnostics.CanClose == true;

    private static string FormatUtc(DateTime value) =>
        value == DateTime.MinValue
            ? string.Empty
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

    private sealed record InspectorBatchDefinition(
        string Category,
        bool IsFinalBatch,
        Func<FileInspectorSelection, CancellationToken, Task<FileInspectorFieldUpdate[]>> LoadAsync);
}
