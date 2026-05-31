using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Application.Settings;

/// <summary>
/// The complete, persisted application settings snapshot (pane paths, layout, sort, window placement,
/// parallel-execution options). Loaded/saved as a whole via
/// <see cref="WinUiFileManager.Application.Abstractions.ISettingsRepository"/>; immutable, so updates use
/// <c>with</c> expressions. Constructor defaults double as first-run defaults.
/// </summary>
public sealed record AppSettings
{
    public AppSettings(
        bool parallelExecutionEnabled = false,
        int maxDegreeOfParallelism = 4,
        NormalizedPath? lastLeftPanePath = null,
        NormalizedPath? lastRightPanePath = null,
        string lastActivePane = "Left",
        bool inspectorVisible = true,
        double inspectorWidth = 340d,
        double leftPaneWidth = 600d,
        PaneColumnLayout? leftPaneColumns = null,
        PaneColumnLayout? rightPaneColumns = null,
        SortState? leftPaneSort = null,
        SortState? rightPaneSort = null,
        WindowPlacement? mainWindowPlacement = null)
    {
        ParallelExecutionEnabled = parallelExecutionEnabled;
        MaxDegreeOfParallelism = maxDegreeOfParallelism;
        LastLeftPanePath = lastLeftPanePath;
        LastRightPanePath = lastRightPanePath;
        LastActivePane = lastActivePane;
        InspectorVisible = inspectorVisible;
        InspectorWidth = inspectorWidth;
        LeftPaneWidth = leftPaneWidth;
        LeftPaneColumns = leftPaneColumns ?? PaneColumnLayout.Default;
        RightPaneColumns = rightPaneColumns ?? PaneColumnLayout.Default;
        LeftPaneSort = leftPaneSort ?? SortState.Default;
        RightPaneSort = rightPaneSort ?? SortState.Default;
        MainWindowPlacement = mainWindowPlacement ?? WindowPlacement.Default;
    }

    /// <summary>Whether bulk file operations may run in parallel.</summary>
    public bool ParallelExecutionEnabled { get; init; }

    /// <summary>Max concurrent operations when <see cref="ParallelExecutionEnabled"/> is set.</summary>
    public int MaxDegreeOfParallelism { get; init; }

    /// <summary>Last directory shown in the left pane, restored on next launch; <see langword="null"/> if none.</summary>
    public NormalizedPath? LastLeftPanePath { get; init; }

    /// <summary>Last directory shown in the right pane, restored on next launch; <see langword="null"/> if none.</summary>
    public NormalizedPath? LastRightPanePath { get; init; }

    /// <summary>Which pane was active last (e.g. <c>"Left"</c>/<c>"Right"</c>).</summary>
    public string LastActivePane { get; init; }

    /// <summary>Whether the inspector pane is shown.</summary>
    public bool InspectorVisible { get; init; }

    /// <summary>Inspector pane width in DIPs.</summary>
    public double InspectorWidth { get; init; }

    /// <summary>Left pane width in DIPs (the right pane fills the remainder).</summary>
    public double LeftPaneWidth { get; init; }

    /// <summary>Persisted column widths for the left pane.</summary>
    public PaneColumnLayout LeftPaneColumns { get; init; }

    /// <summary>Persisted column widths for the right pane.</summary>
    public PaneColumnLayout RightPaneColumns { get; init; }

    /// <summary>Persisted sort column/direction for the left pane.</summary>
    public SortState LeftPaneSort { get; init; }

    /// <summary>Persisted sort column/direction for the right pane.</summary>
    public SortState RightPaneSort { get; init; }

    /// <summary>Persisted main-window position/size/maximized state.</summary>
    public WindowPlacement MainWindowPlacement { get; init; }
}
