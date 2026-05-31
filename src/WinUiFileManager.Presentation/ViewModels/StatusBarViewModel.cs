namespace WinUiFileManager.Presentation.ViewModels;

/// <summary>
/// Observable state for the bottom status bar: active pane name, item/selection counts, aggregated selected size,
/// and a transient status message. A passive data holder — values are pushed in by other view models/behaviors;
/// it neither subscribes to messages nor owns disposable resources.
/// </summary>
public sealed partial class StatusBarViewModel : ObservableObject
{
    /// <summary>Display name of the currently active pane.</summary>
    [ObservableProperty]
    public partial string ActivePaneName { get; set; } = string.Empty;

    /// <summary>Total number of items in the active pane.</summary>
    [ObservableProperty]
    public partial int ItemCount { get; set; }

    /// <summary>Number of currently selected items.</summary>
    [ObservableProperty]
    public partial int SelectedCount { get; set; }

    /// <summary>Human-readable aggregated size of the selection (pre-formatted string).</summary>
    [ObservableProperty]
    public partial string SelectedSize { get; set; } = string.Empty;

    /// <summary>Optional transient status text; <c>null</c> when there is nothing to show.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }
}
