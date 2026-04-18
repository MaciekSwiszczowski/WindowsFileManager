namespace WinUiFileManager.Infrastructure.Persistence;

internal sealed record SettingsDto(
    bool ParallelExecutionEnabled,
    int MaxDegreeOfParallelism,
    string? LastLeftPanePath,
    string? LastRightPanePath,
    string LastActivePane,
    bool InspectorVisible,
    double InspectorWidth);
