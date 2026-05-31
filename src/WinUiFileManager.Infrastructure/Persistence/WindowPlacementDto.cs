namespace WinUiFileManager.Infrastructure.Persistence;

/// <summary>
/// Serialization-only DTO for the persisted main-window placement (restored bounds, maximized flag, owning
/// monitor). Maps to/from the domain <c>WindowPlacement</c> in <see cref="JsonSettingsRepository"/>; non-positive
/// width/height are corrected to defaults during the DTO-&gt;domain mapping.
/// </summary>
internal sealed record WindowPlacementDto
{
    /// <summary>Left edge of the restored window, in screen pixels.</summary>
    public int X { get; init; }

    /// <summary>Top edge of the restored window, in screen pixels.</summary>
    public int Y { get; init; }

    /// <summary>Restored window width; defaulted on load if not positive.</summary>
    public int Width { get; init; }

    /// <summary>Restored window height; defaulted on load if not positive.</summary>
    public int Height { get; init; }

    /// <summary>Whether the window was maximized when saved.</summary>
    public bool IsMaximized { get; init; }

    /// <summary>The <c>\\.\DISPLAYn</c> device name of the owning monitor, or <see langword="null"/> if unknown.</summary>
    public string? DisplayDeviceName { get; init; }
}
