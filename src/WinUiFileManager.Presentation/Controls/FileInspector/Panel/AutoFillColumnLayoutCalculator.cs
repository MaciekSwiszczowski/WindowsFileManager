namespace WinUiFileManager.Presentation.Controls.FileInspector.Panel;

/// <summary>
/// Pure column-fill math for <see cref="AutoFillColumnsPanel"/>, extracted so it can be data-driven unit tested without
/// a UI thread. Given a viewport (<see cref="AutoFillColumnLayoutInput"/>) and a callback that measures card heights at
/// a candidate column width, it picks a column count and produces per-card placements.
/// </summary>
/// <remarks>
/// <para>
/// Two rules define the layout:
/// </para>
/// <para>
/// <b>Fewest columns that fit.</b> The column count is the smallest number of columns whose column-major layout fits
/// the viewport height, capped by how many <see cref="AutoFillColumnLayoutInput.DesiredColumnWidth"/>-wide columns fit
/// the width and by the card count. So once the cards already fit vertically, widening the viewport does not add a
/// column — the existing columns just stretch; columns are added only when the content would otherwise overflow. When
/// the viewport height is unknown, or nothing fits, it falls back to the width-derived maximum.
/// </para>
/// <para>
/// <b>Balanced contiguous fill.</b> Cards are distributed by <see cref="DistributeColumns"/>: with <c>n</c> cards over
/// <c>c</c> columns the first <c>n % c</c> columns get <c>ceil(n/c)</c> cards and the rest get <c>floor(n/c)</c>, filled
/// in declaration order. Because the column count is clamped to at most <c>n</c>, every column gets at least one card —
/// this is what prevents the earlier "fill column 0 completely first" scheme from leaving a trailing empty column (e.g.
/// 9 cards into 4 columns is 3/2/2/2, not 3/3/3/0). Reading order stays natural (down a column, then the next) and a
/// long value only grows its own column.
/// </para>
/// </remarks>
internal static class AutoFillColumnLayoutCalculator
{
    // Tolerance when comparing a laid-out column height against the viewport height, so a sub-pixel overshoot at an
    // exact fit does not needlessly add a column (which would also flip the auto scrollbar on and off).
    private const double LayoutEpsilon = 0.5;

    /// <summary>
    /// Computes the column layout for the given viewport.
    /// </summary>
    /// <param name="input">Viewport width/height and spacing.</param>
    /// <param name="measureCardHeights">
    /// Measures the cards at a candidate column width and returns the heights of the cards that occupy a slot, in
    /// declaration order. Called several times (once per evaluated column count, plus a final time at the chosen width);
    /// the final call is at the chosen column width so callers can rely on their cards being measured at that width.
    /// </param>
    public static AutoFillColumnLayoutResult Calculate(
        AutoFillColumnLayoutInput input,
        Func<double, IReadOnlyList<double>> measureCardHeights)
    {
        var widthDerivedColumns = ComputeColumnCount(input.AvailableWidth, input.DesiredColumnWidth, input.ColumnSpacing);

        // Measure once to learn how many cards occupy a slot. Occupancy is width-independent (a filtered-out card is
        // zero-height at any width, so its height simply never enters the returned list), so this count is stable.
        var probeWidth = ComputeColumnWidth(input.AvailableWidth, widthDerivedColumns, input.ColumnSpacing, input.DesiredColumnWidth);
        var cardCount = measureCardHeights(probeWidth).Count;
        if (cardCount == 0)
        {
            var emptyColumnWidth = ComputeColumnWidth(input.AvailableWidth, 1, input.ColumnSpacing, input.DesiredColumnWidth);
            return new AutoFillColumnLayoutResult(1, emptyColumnWidth, ResolveTotalWidth(input, 1), 0, Array.Empty<AutoFillCardPlacement>());
        }

        var maxColumns = ClampColumnCount(widthDerivedColumns, cardCount);
        var columnCount = SelectColumnCount(input, measureCardHeights, maxColumns);

        // Final measure at the chosen width so the placements (and the panel's child DesiredSize) reflect the column
        // width actually used.
        var columnWidth = ComputeColumnWidth(input.AvailableWidth, columnCount, input.ColumnSpacing, input.DesiredColumnWidth);
        var heights = measureCardHeights(columnWidth);
        var placements = BuildPlacements(heights, columnCount, columnWidth, input.ColumnSpacing, input.RowSpacing, out var totalHeight);
        return new AutoFillColumnLayoutResult(columnCount, columnWidth, ResolveTotalWidth(input, columnCount), totalHeight, placements);
    }

    // Picks the fewest columns whose column-major layout fits the viewport height. Falls back to the width-derived
    // maximum when the height is unknown or even the maximum does not fit (the content then scrolls).
    private static int SelectColumnCount(
        AutoFillColumnLayoutInput input,
        Func<double, IReadOnlyList<double>> measureCardHeights,
        int maxColumns)
    {
        var canFit = !double.IsInfinity(input.ViewportHeight) && input.ViewportHeight > 0;
        if (!canFit)
        {
            return maxColumns;
        }

        for (var columns = 1; columns <= maxColumns; columns++)
        {
            var columnWidth = ComputeColumnWidth(input.AvailableWidth, columns, input.ColumnSpacing, input.DesiredColumnWidth);
            var layoutHeight = ComputeLayoutHeight(measureCardHeights(columnWidth), columns, input.RowSpacing);
            if (layoutHeight <= input.ViewportHeight + LayoutEpsilon)
            {
                return columns;
            }
        }

        return maxColumns;
    }

    /// <summary>
    /// Distributes <paramref name="cardCount"/> cards across <paramref name="columnCount"/> columns, returning the
    /// zero-based column index for each card in declaration order. The first <c>cardCount % columnCount</c> columns get
    /// one extra card; cards fill each column before moving to the next (contiguous, balanced). When
    /// <paramref name="columnCount"/> is at most <paramref name="cardCount"/> every column receives at least one card.
    /// </summary>
    internal static int[] DistributeColumns(int cardCount, int columnCount)
    {
        var result = new int[Math.Max(0, cardCount)];
        if (cardCount <= 0)
        {
            return result;
        }

        var columns = Math.Max(1, Math.Min(columnCount, cardCount));
        var basePerColumn = cardCount / columns;
        var remainder = cardCount % columns;
        var index = 0;
        for (var column = 0; column < columns; column++)
        {
            var cardsInColumn = basePerColumn + (column < remainder ? 1 : 0);
            for (var k = 0; k < cardsInColumn; k++)
            {
                result[index++] = column;
            }
        }

        return result;
    }

    // Lays the heights out column-major into the given column count and returns the tallest column's height.
    private static double ComputeLayoutHeight(IReadOnlyList<double> heights, int columnCount, double rowSpacing)
    {
        var columns = Math.Max(1, Math.Min(columnCount, Math.Max(1, heights.Count)));
        var assignment = DistributeColumns(heights.Count, columns);
        var columnHeights = new double[columns];
        var columnCounts = new int[columns];
        for (var i = 0; i < heights.Count; i++)
        {
            var column = assignment[i];
            if (columnCounts[column] > 0)
            {
                columnHeights[column] += rowSpacing;
            }

            columnHeights[column] += heights[i];
            columnCounts[column]++;
        }

        return Max(columnHeights);
    }

    // Lays the heights out column-major and produces per-card placement rectangles (and the total height).
    private static AutoFillCardPlacement[] BuildPlacements(
        IReadOnlyList<double> heights,
        int columnCount,
        double columnWidth,
        double columnSpacing,
        double rowSpacing,
        out double totalHeight)
    {
        var columns = Math.Max(1, Math.Min(columnCount, Math.Max(1, heights.Count)));
        var assignment = DistributeColumns(heights.Count, columns);
        var columnHeights = new double[columns];
        var columnCounts = new int[columns];
        var placements = new AutoFillCardPlacement[heights.Count];
        for (var i = 0; i < heights.Count; i++)
        {
            var column = assignment[i];
            if (columnCounts[column] > 0)
            {
                columnHeights[column] += rowSpacing;
            }

            var x = column * (columnWidth + columnSpacing);
            var y = columnHeights[column];
            placements[i] = new AutoFillCardPlacement(column, x, y, columnWidth, heights[i]);
            columnHeights[column] += heights[i];
            columnCounts[column]++;
        }

        totalHeight = Max(columnHeights);
        return placements;
    }

    /// <summary>Largest number of <see cref="AutoFillColumnLayoutInput.DesiredColumnWidth"/>-wide columns that fit the width (at least one).</summary>
    internal static int ComputeColumnCount(double availableWidth, double desiredColumnWidth, double columnSpacing)
    {
        if (double.IsInfinity(availableWidth) || availableWidth <= 0 || desiredColumnWidth <= 0)
        {
            return 1;
        }

        var count = (int)Math.Floor((availableWidth + columnSpacing) / (desiredColumnWidth + columnSpacing));
        return Math.Max(1, count);
    }

    /// <summary>Uniform column width that divides the available width across <paramref name="columnCount"/> columns.</summary>
    internal static double ComputeColumnWidth(double availableWidth, int columnCount, double columnSpacing, double desiredColumnWidth)
    {
        if (double.IsInfinity(availableWidth) || availableWidth <= 0)
        {
            return desiredColumnWidth;
        }

        var width = (availableWidth - (columnSpacing * (columnCount - 1))) / columnCount;
        return Math.Max(0, width);
    }

    // Never use more columns than there are cards to fill them, so a small set of cards stays wide rather than spreading
    // thin across empty columns. At least one column so the layout arrays are non-empty.
    private static int ClampColumnCount(int columnCount, int cardCount) =>
        Math.Max(1, Math.Min(columnCount, cardCount));

    private static double ResolveTotalWidth(AutoFillColumnLayoutInput input, int columnCount)
    {
        if (!double.IsInfinity(input.AvailableWidth) && input.AvailableWidth > 0)
        {
            return input.AvailableWidth;
        }

        var columnWidth = ComputeColumnWidth(input.AvailableWidth, columnCount, input.ColumnSpacing, input.DesiredColumnWidth);
        return columnWidth * columnCount + input.ColumnSpacing * (columnCount - 1);
    }

    private static double Max(double[] values)
    {
        double max = 0;
        foreach (var value in values)
        {
            max = Math.Max(max, value);
        }

        return max;
    }
}
