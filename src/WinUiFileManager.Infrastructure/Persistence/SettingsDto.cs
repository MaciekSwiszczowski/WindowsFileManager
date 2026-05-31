namespace WinUiFileManager.Infrastructure.Persistence;

/// <summary>
/// Serialization-only DTO mirroring the JSON shape of the persisted settings file. Decoupled from the domain
/// <c>AppSettings</c> so the on-disk schema can evolve independently; <see cref="JsonSettingsRepository"/> maps
/// between the two. Property defaults here act as the schema defaults for fields missing from older files.
/// </summary>
/// <remarks>Reference-type members are nullable to distinguish "absent in file" from a real value during mapping.</remarks>
internal sealed record SettingsDto
{
    /// <summary>Whether bulk operations run in parallel.</summary>
    public bool ParallelExecutionEnabled { get; init; }

    /// <summary>Degree of parallelism for bulk operations; defaults to 4 when absent.</summary>
    public int MaxDegreeOfParallelism { get; init; } = 4;

    /// <summary>Last folder shown in the left pane (display path), or <see langword="null"/> if never set.</summary>
    public string? LastLeftPanePath { get; init; }

    /// <summary>Last folder shown in the right pane (display path), or <see langword="null"/> if never set.</summary>
    public string? LastRightPanePath { get; init; }

    /// <summary>Identity of the pane that was active last session; defaults to "Left".</summary>
    public string LastActivePane { get; init; } = "Left";

    /// <summary>Whether the inspector panel is visible; defaults to <see langword="true"/>.</summary>
    public bool InspectorVisible { get; init; } = true;

    /// <summary>Inspector panel width in DIPs; defaults to 340.</summary>
    public double InspectorWidth { get; init; } = 340d;

    /// <summary>Left pane width in DIPs, or <see langword="null"/> to use the default on load.</summary>
    public double? LeftPaneWidth { get; init; }

    /// <summary>Persisted left-pane column widths, or <see langword="null"/> for defaults.</summary>
    public PaneColumnLayoutDto? LeftPaneColumns { get; init; }

    /// <summary>Persisted right-pane column widths, or <see langword="null"/> for defaults.</summary>
    public PaneColumnLayoutDto? RightPaneColumns { get; init; }

    /// <summary>Persisted left-pane sort, or <see langword="null"/> for defaults.</summary>
    public SortStateDto? LeftPaneSort { get; init; }

    /// <summary>Persisted right-pane sort, or <see langword="null"/> for defaults.</summary>
    public SortStateDto? RightPaneSort { get; init; }

    /// <summary>Persisted main-window placement, or <see langword="null"/> when not yet saved.</summary>
    public WindowPlacementDto? MainWindowPlacement { get; init; }
}
