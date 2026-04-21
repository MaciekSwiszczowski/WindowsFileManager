using System.Runtime.InteropServices;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Settings;

[StructLayout(LayoutKind.Auto)]
public readonly record struct PersistPaneStateRequest(
    NormalizedPath? LeftPanePath,
    NormalizedPath? RightPanePath,
    PaneId ActivePane,
    bool InspectorVisible,
    double InspectorWidth,
    double LeftPaneWidth,
    PaneColumnLayout LeftPaneColumns,
    PaneColumnLayout RightPaneColumns,
    SortState LeftPaneSort,
    SortState RightPaneSort,
    WindowPlacement MainWindowPlacement);
