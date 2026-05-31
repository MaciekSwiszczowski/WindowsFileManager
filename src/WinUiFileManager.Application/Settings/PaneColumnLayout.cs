using System.Runtime.InteropServices;

namespace WinUiFileManager.Application.Settings;

/// <summary>
/// Persisted per-pane file-table column widths (in DIPs), part of <see cref="AppSettings"/>. A value
/// struct so it is stored inline; <c>[StructLayout(Auto)]</c> lets the runtime pack the fields.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PaneColumnLayout(
    double NameWidth,
    double ExtensionWidth,
    double SizeWidth,
    double ModifiedWidth,
    double AttributesWidth)
{
    /// <summary>Default column widths used on first run or when none are persisted.</summary>
    public static PaneColumnLayout Default { get; } = new(
        NameWidth: 320d,
        ExtensionWidth: 40d,
        SizeWidth: 70d,
        ModifiedWidth: 120d,
        AttributesWidth: 50d);
}
