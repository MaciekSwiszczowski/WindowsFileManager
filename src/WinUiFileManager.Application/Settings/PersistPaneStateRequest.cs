using System.Runtime.InteropServices;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Application.Settings;

/// <summary>
/// Input payload for <see cref="PersistPaneStateCommandHandler"/>: a snapshot of the dual-pane UI state
/// to write into <see cref="AppSettings"/>. A value struct (<c>[StructLayout(Auto)]</c>) gathered by the
/// shell before persisting.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PersistPaneStateRequest(
    NormalizedPath? LeftPanePath,
    NormalizedPath? RightPanePath,
    string ActivePane,
    bool InspectorVisible,
    double InspectorWidth,
    double LeftPaneWidth,
    PaneColumnLayout LeftPaneColumns,
    PaneColumnLayout RightPaneColumns,
    SortState LeftPaneSort,
    SortState RightPaneSort,
    WindowPlacement MainWindowPlacement);
