namespace WinUiFileManager.Infrastructure.Persistence;

internal sealed record SettingsDto
{
    public SettingsDto(
        bool parallelExecutionEnabled,
        int maxDegreeOfParallelism,
        string? lastLeftPanePath,
        string? lastRightPanePath,
        string lastActivePane,
        bool inspectorVisible,
        double inspectorWidth)
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

    public string? LastLeftPanePath { get; init; }

    public string? LastRightPanePath { get; init; }

    public string LastActivePane { get; init; }

    public bool InspectorVisible { get; init; }

    public double InspectorWidth { get; init; }
}
