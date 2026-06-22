using System.Runtime.InteropServices;

namespace WinUiFileManager.Presentation.Controls.FileInspector.Panel;

/// <summary>
/// The computed position of one occupying card within an <see cref="AutoFillColumnLayoutResult"/>: which column it
/// landed in and the rectangle (in panel-local coordinates) it should be arranged into. Pure data (no WinUI types) so
/// it can be asserted on in unit tests.
/// </summary>
/// <param name="Column">Zero-based column index the card was assigned to.</param>
/// <param name="X">Left edge of the card.</param>
/// <param name="Y">Top edge of the card within its column's top-aligned stack.</param>
/// <param name="Width">Card width (the uniform column width).</param>
/// <param name="Height">Card height as measured at the chosen column width.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct AutoFillCardPlacement(
    int Column,
    double X,
    double Y,
    double Width,
    double Height);
