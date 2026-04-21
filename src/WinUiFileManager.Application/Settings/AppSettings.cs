using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Settings;

public sealed record AppSettings
{
    public AppSettings(
        bool parallelExecutionEnabled = false,
        int maxDegreeOfParallelism = 4,
        NormalizedPath? lastLeftPanePath = null,
        NormalizedPath? lastRightPanePath = null,
        PaneId lastActivePane = PaneId.Left,
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

    public bool ParallelExecutionEnabled { get; init; }

    public int MaxDegreeOfParallelism { get; init; }

    public NormalizedPath? LastLeftPanePath { get; init; }

    public NormalizedPath? LastRightPanePath { get; init; }

    public PaneId LastActivePane { get; init; }

    public bool InspectorVisible { get; init; }

    public double InspectorWidth { get; init; }

    public double LeftPaneWidth { get; init; }

    public PaneColumnLayout LeftPaneColumns { get; init; }

    public PaneColumnLayout RightPaneColumns { get; init; }

    public SortState LeftPaneSort { get; init; }

    public SortState RightPaneSort { get; init; }

    public WindowPlacement MainWindowPlacement { get; init; }
}
