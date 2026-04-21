namespace WinUiFileManager.Infrastructure.Persistence;

internal sealed record SettingsDto
{
    public bool ParallelExecutionEnabled { get; init; }

    public int MaxDegreeOfParallelism { get; init; } = 4;

    public string? LastLeftPanePath { get; init; }

    public string? LastRightPanePath { get; init; }

    public string LastActivePane { get; init; } = "Left";

    public bool InspectorVisible { get; init; } = true;

    public double InspectorWidth { get; init; } = 340d;

    public double? LeftPaneWidth { get; init; }

    public PaneColumnLayoutDto? LeftPaneColumns { get; init; }

    public PaneColumnLayoutDto? RightPaneColumns { get; init; }

    public SortStateDto? LeftPaneSort { get; init; }

    public SortStateDto? RightPaneSort { get; init; }

    public WindowPlacementDto? MainWindowPlacement { get; init; }
}
