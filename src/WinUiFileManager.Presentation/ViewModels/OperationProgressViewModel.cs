using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUiFileManager.Domain.Events;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class OperationProgressViewModel : ObservableObject
{
    private readonly CancellationTokenSource _cts = new();

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
    public partial bool CanCancel { get; set; } = true;

    public CancellationToken CancellationToken => _cts.Token;

    [RelayCommand]
    private void Cancel()
    {
        if (!CanCancel)
            return;

        _cts.Cancel();
        CanCancel = false;
    }

    public void ReportProgress(OperationProgressEvent e)
    {
        TotalItems = e.TotalItems;
        CompletedItems = e.CompletedItems;
        CurrentItemPath = e.CurrentItemPath?.DisplayPath;
        ProgressPercentage = e.TotalItems > 0
            ? (double)e.CompletedItems / e.TotalItems * 100.0
            : 0.0;
    }
}
