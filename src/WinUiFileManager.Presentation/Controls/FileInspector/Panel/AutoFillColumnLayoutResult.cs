namespace WinUiFileManager.Presentation.Controls.FileInspector.Panel;

/// <summary>
/// The result of <see cref="AutoFillColumnLayoutCalculator"/>: the chosen column count and width, the panel's desired
/// size, and where each occupying card should be arranged. <see cref="Placements"/> is ordered to match the occupying
/// cards in declaration order, so the panel can zip it against its (occupying) children during arrange.
/// </summary>
/// <param name="ColumnCount">Number of columns actually used (every one is non-empty when there is at least one card).</param>
/// <param name="ColumnWidth">Uniform width given to every column.</param>
/// <param name="Width">Total width the panel reports (the available width, or the column run when width is unconstrained).</param>
/// <param name="Height">Total height the panel reports (the tallest column).</param>
/// <param name="Placements">Per-card placement, in occupying-card declaration order.</param>
internal sealed record AutoFillColumnLayoutResult(
    int ColumnCount,
    double ColumnWidth,
    double Width,
    double Height,
    IReadOnlyList<AutoFillCardPlacement> Placements);
