using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FileInspectorViewModel : ObservableObject
{
    private readonly IFileIdentityService _fileIdentityService;
    private readonly IClipboardService _clipboardService;
    private readonly ILogger<FileInspectorViewModel> _logger;

    private CancellationTokenSource? _loadCancellation;
    private int _selectionVersion;

    [ObservableProperty]
    public partial bool HasItem { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingDetails { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FullPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Type { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Extension { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Size { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CreationTimeUtc { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastWriteTimeUtc { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Attributes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FileId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InUse { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LockBy { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LockPid { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LockSvc { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Usage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CanSw { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CanCls { get; set; } = string.Empty;

    public Visibility DetailsVisibility => HasItem ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility => HasItem ? Visibility.Collapsed : Visibility.Visible;

    public FileInspectorViewModel(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        ILogger<FileInspectorViewModel> logger)
    {
        _fileIdentityService = fileIdentityService;
        _clipboardService = clipboardService;
        _logger = logger;
    }

    public async Task UpdateSelectionAsync(
        IReadOnlyList<FileEntryViewModel> selectedEntries,
        bool isPaneLoading,
        CancellationToken cancellationToken = default)
    {
        CancelPendingLoad();

        if (isPaneLoading)
        {
            Clear("Pane is loading...");
            return;
        }

        if (selectedEntries.Count == 0)
        {
            Clear(string.Empty);
            return;
        }

        if (selectedEntries.Count != 1)
        {
            Clear($"{selectedEntries.Count} items selected.");
            return;
        }

        var entry = selectedEntries[0];
        if (entry.IsParentEntry)
        {
            Clear(string.Empty);
            return;
        }

        _selectionVersion++;
        var currentVersion = _selectionVersion;
        _loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loadToken = _loadCancellation.Token;

        ClearFieldValues();
        HasItem = false;
        IsLoadingDetails = true;
        StatusMessage = "Loading details...";

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), loadToken);

            PopulateBasicFields(entry);
            HasItem = true;
            StatusMessage = string.Empty;

            var fileIdTask = _fileIdentityService.GetFileIdAsync(entry.Model.FullPath.DisplayPath, loadToken);
            var lockDiagnosticsTask = _fileIdentityService.GetLockDiagnosticsAsync(entry.Model.FullPath.DisplayPath, loadToken);

            var fileIdLoaded = await TryApplyWithTimeoutAsync(
                fileIdTask,
                TimeSpan.FromSeconds(5),
                loadToken,
                fileId =>
                {
                    FileId = fileId == NtfsFileId.None ? "Unavailable" : fileId.HexDisplay;
                });

            if (!fileIdLoaded)
            {
                if (currentVersion != _selectionVersion || loadToken.IsCancellationRequested)
                {
                    return;
                }

                FileId = "Unavailable";
            }

            var lockDiagnosticsLoaded = await TryApplyWithTimeoutAsync(
                lockDiagnosticsTask,
                TimeSpan.FromSeconds(5),
                loadToken,
                PopulateLockDiagnosticsFields);

            if (!lockDiagnosticsLoaded)
            {
                if (currentVersion != _selectionVersion || loadToken.IsCancellationRequested)
                {
                    return;
                }

                InUse = "N/A";
                LockBy = "N/A";
                LockPid = "N/A";
                LockSvc = "N/A";
                Usage = "N/A";
                CanSw = "N/A";
                CanCls = "N/A";
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Inspector update canceled for {Path}", entry.Model.FullPath.DisplayPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load FileId for {Path}", entry.Model.FullPath.DisplayPath);
            if (currentVersion == _selectionVersion)
            {
                FileId = "Unavailable";
            }
        }
        finally
        {
            if (_loadCancellation is not null && _loadCancellation.Token == loadToken)
            {
                _loadCancellation.Dispose();
                _loadCancellation = null;
            }

            if (currentVersion == _selectionVersion)
            {
                IsLoadingDetails = false;
            }
        }
    }

    [RelayCommand]
    private async Task CopyAllAsync()
    {
        if (!HasItem)
        {
            return;
        }

        var builder = new StringBuilder();
        AppendLine(builder, "Name", Name);
        AppendLine(builder, "Full Path", FullPath);
        AppendLine(builder, "Type", Type);
        AppendLine(builder, "Extension", Extension);
        AppendLine(builder, "Size", Size);
        AppendLine(builder, "Creation Time (UTC)", CreationTimeUtc);
        AppendLine(builder, "Last Write Time (UTC)", LastWriteTimeUtc);
        AppendLine(builder, "Attributes", Attributes);
        AppendLine(builder, "NTFS File/Folder ID", FileId);
        AppendLine(builder, "In Use", InUse);
        AppendLine(builder, "Locked By", LockBy);
        AppendLine(builder, "Lock PIDs", LockPid);
        AppendLine(builder, "Lock Services", LockSvc);
        AppendLine(builder, "Usage", Usage);
        AppendLine(builder, "Can Switch To", CanSw);
        AppendLine(builder, "Can Close", CanCls);

        await _clipboardService.SetTextAsync(builder.ToString().TrimEnd(), CancellationToken.None);
    }

    public void Clear(string statusMessage)
    {
        _selectionVersion++;
        CancelPendingLoad();
        ClearFieldValues();
        HasItem = false;
        IsLoadingDetails = false;
        StatusMessage = statusMessage;
    }

    partial void OnHasItemChanged(bool value)
    {
        OnPropertyChanged(nameof(DetailsVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    private void PopulateBasicFields(FileEntryViewModel entry)
    {
        Name = entry.Name;
        FullPath = entry.Model.FullPath.DisplayPath;
        Type = entry.Kind == ItemKind.Directory ? "Folder" : "File";
        Extension = entry.Extension;
        Size = FormatSize(entry.SizeBytes);
        CreationTimeUtc = FormatUtc(entry.CreationTimeUtc);
        LastWriteTimeUtc = FormatUtc(entry.LastWriteTimeUtc);
        Attributes = entry.Attributes;
        FileId = "Loading...";
        InUse = "Loading...";
        LockBy = "Loading...";
        LockPid = "Loading...";
        LockSvc = "Loading...";
        Usage = "Loading...";
        CanSw = "Loading...";
        CanCls = "Loading...";
    }

    private void CancelPendingLoad()
    {
        if (_loadCancellation is null)
        {
            return;
        }

        _loadCancellation.Cancel();
        _loadCancellation.Dispose();
        _loadCancellation = null;
    }

    private void ClearFieldValues()
    {
        Name = string.Empty;
        FullPath = string.Empty;
        Type = string.Empty;
        Extension = string.Empty;
        Size = string.Empty;
        CreationTimeUtc = string.Empty;
        LastWriteTimeUtc = string.Empty;
        Attributes = string.Empty;
        FileId = string.Empty;
        InUse = string.Empty;
        LockBy = string.Empty;
        LockPid = string.Empty;
        LockSvc = string.Empty;
        Usage = string.Empty;
        CanSw = string.Empty;
        CanCls = string.Empty;
    }

    private static void AppendLine(StringBuilder builder, string label, string value) =>
        builder.Append(label).Append(": ").AppendLine(value);

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

    private async Task<bool> TryApplyWithTimeoutAsync<T>(
        Task<T> task,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<T> apply)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout, cancellationToken));
        if (completed != task)
        {
            return false;
        }

        apply(await task);
        return true;
    }

    private void PopulateLockDiagnosticsFields(FileLockDiagnostics diagnostics)
    {
        InUse = FormatBoolean(diagnostics.InUse);
        LockBy = diagnostics.LockBy.Count == 0 ? "N/A" : string.Join(Environment.NewLine, diagnostics.LockBy);
        LockPid = diagnostics.LockPids.Count == 0 ? "N/A" : string.Join(", ", diagnostics.LockPids);
        LockSvc = diagnostics.LockServices.Count == 0 ? "N/A" : string.Join(", ", diagnostics.LockServices);
        Usage = string.IsNullOrWhiteSpace(diagnostics.Usage) ? "N/A" : diagnostics.Usage;
        CanSw = FormatBoolean(diagnostics.CanSwitchTo);
        CanCls = FormatBoolean(diagnostics.CanClose);
    }

    private static string FormatBoolean(bool? value) =>
        value switch
        {
            true => "Yes",
            false => "No",
            _ => "N/A"
        };
}
