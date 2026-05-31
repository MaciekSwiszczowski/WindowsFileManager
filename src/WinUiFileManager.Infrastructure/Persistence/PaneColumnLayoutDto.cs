namespace WinUiFileManager.Infrastructure.Persistence;

/// <summary>
/// Serialization-only DTO for a pane's persisted column widths (in DIPs). Maps to/from the domain
/// <c>PaneColumnLayout</c> in <see cref="JsonSettingsRepository"/>; any non-positive width is replaced with the
/// corresponding <c>PaneColumnLayout.Default</c> value during the DTO-&gt;domain mapping.
/// </summary>
internal sealed record PaneColumnLayoutDto
{
    /// <summary>Width of the Name column.</summary>
    public double NameWidth { get; init; }

    /// <summary>Width of the Extension column.</summary>
    public double ExtensionWidth { get; init; }

    /// <summary>Width of the Size column.</summary>
    public double SizeWidth { get; init; }

    /// <summary>Width of the Modified column.</summary>
    public double ModifiedWidth { get; init; }

    /// <summary>Width of the Attributes column.</summary>
    public double AttributesWidth { get; init; }
}
