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
        double inspectorWidth = 340d)
    {
        ParallelExecutionEnabled = parallelExecutionEnabled;
        MaxDegreeOfParallelism = maxDegreeOfParallelism;
        LastLeftPanePath = lastLeftPanePath;
        LastRightPanePath = lastRightPanePath;
        LastActivePane = lastActivePane;
        InspectorVisible = inspectorVisible;
        InspectorWidth = inspectorWidth;
    }

    public bool ParallelExecutionEnabled { get; init; }

    public int MaxDegreeOfParallelism { get; init; }

    public NormalizedPath? LastLeftPanePath { get; init; }

    public NormalizedPath? LastRightPanePath { get; init; }

    public PaneId LastActivePane { get; init; }

    public bool InspectorVisible { get; init; }

    public double InspectorWidth { get; init; }
}
