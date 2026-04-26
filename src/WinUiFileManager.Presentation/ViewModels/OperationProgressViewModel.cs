namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class OperationProgressViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    public partial string OperationName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CurrentItemPath { get; set; }

    [ObservableProperty]
    public partial int TotalItems { get; set; }

    [ObservableProperty]
    public partial int CompletedItems { get; set; }

    [ObservableProperty]
    public partial double ProgressPercentage { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; } = "Preparing operation...";

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanCancel { get; set; } = true;

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial Visibility PanelVisibility { get; set; } = Visibility.Collapsed;

    public CancellationToken CancellationToken => _cts?.Token ?? CancellationToken.None;

    public void Start(OperationType type)
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        OperationName = GetOperationName(type);
        CurrentItemPath = null;
        TotalItems = 0;
        CompletedItems = 0;
        ProgressPercentage = 0d;
        ProgressText = "Preparing operation...";
        StatusMessage = $"{OperationName} in progress";
        CanCancel = true;
        IsVisible = true;
        IsRunning = true;
        PanelVisibility = Visibility.Visible;
    }

    public void Finish()
    {
        CanCancel = false;
        IsRunning = false;
    }

    public void Reset()
    {
        _cts?.Dispose();
        _cts = null;

        CurrentItemPath = null;
        TotalItems = 0;
        CompletedItems = 0;
        ProgressPercentage = 0d;
        ProgressText = "Preparing operation...";
        StatusMessage = string.Empty;
        CanCancel = false;
        IsVisible = false;
        IsRunning = false;
        PanelVisibility = Visibility.Collapsed;
    }

    [RelayCommand]
    private void Cancel()
    {
        if (!CanCancel || _cts is null)
        {
            return;
        }

        _cts.Cancel();
        CanCancel = false;
    }

    public void ReportProgress(OperationProgressEvent e)
    {
        OperationName = GetOperationName(e.Type);
        TotalItems = e.TotalItems;
        CompletedItems = e.CompletedItems;
        CurrentItemPath = e.CurrentItemPath?.DisplayPath;
        ProgressPercentage = e.TotalItems > 0
            ? (double)e.CompletedItems / e.TotalItems * 100.0
            : 0.0;
        ProgressText = e.TotalItems > 0
            ? $"Processed {e.CompletedItems} of {e.TotalItems} items"
            : "Preparing operation...";
        StatusMessage = e.StatusMessage ?? $"{OperationName} in progress";
    }

    private static string GetOperationName(OperationType type) =>
        type switch
        {
            OperationType.Copy => "Copy",
            OperationType.Move => "Move",
            OperationType.Delete => "Delete",
            OperationType.CreateFolder => "Create Folder",
            OperationType.Rename => "Rename",
            _ => type.ToString(),
        };
}
