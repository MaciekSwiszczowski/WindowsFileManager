using System.Runtime.InteropServices;

namespace WinUiFileManager.Presentation.Controls.FileInspector.Panel;

/// <summary>
/// Immutable inputs to <see cref="AutoFillColumnLayoutCalculator"/>: the viewport the cards must lay out within plus
/// the spacing knobs. Pure data (no WinUI types) so the column-fill math can be unit tested without a UI thread.
/// </summary>
/// <param name="AvailableWidth">
/// Width the panel was measured with; <see cref="double.PositiveInfinity"/> means unconstrained (column width then
/// falls back to <paramref name="DesiredColumnWidth"/>).
/// </param>
/// <param name="ViewportHeight">
/// Visible height the content should try to fit within before scrolling; <see cref="double.PositiveInfinity"/> means
/// unknown (no scroll viewport yet), which disables the fit search and falls back to the width-derived column count.
/// </param>
/// <param name="DesiredColumnWidth">Target column width; the column count is at most how many of these fit the width.</param>
/// <param name="ColumnSpacing">Horizontal gap between columns.</param>
/// <param name="RowSpacing">Vertical gap between cards stacked within a column.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct AutoFillColumnLayoutInput(
    double AvailableWidth,
    double ViewportHeight,
    double DesiredColumnWidth,
    double ColumnSpacing,
    double RowSpacing);
