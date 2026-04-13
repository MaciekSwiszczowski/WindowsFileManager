using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Settings;

public sealed record AppSettings(
    bool ParallelExecutionEnabled = false,
    int MaxDegreeOfParallelism = 4,
    NormalizedPath? LastLeftPanePath = null,
    NormalizedPath? LastRightPanePath = null,
    PaneId LastActivePane = PaneId.Left);
