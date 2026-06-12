using WinUiFileManager.Presentation.Controls.FileInspector;
using WinUiFileManager.Presentation.Controls.FileInspector.Panel;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class AutoFillColumnLayoutCalculatorTests
{
    [Theory]
    [InlineData(9, 4, "3,2,2,2")]
    [InlineData(9, 3, "3,3,3")]
    [InlineData(9, 5, "2,2,2,2,1")]
    [InlineData(9, 1, "9")]
    [InlineData(10, 4, "3,3,2,2")]
    [InlineData(7, 3, "3,2,2")]
    [InlineData(1, 1, "1")]
    [InlineData(5, 5, "1,1,1,1,1")]
    public void DistributeColumns_IsBalancedContiguousAndLeavesNoEmptyColumn(int cardCount, int columnCount, string expectedCountsCsv)
    {
        // Arrange
        var expectedCounts = ParseCounts(expectedCountsCsv);

        // Act
        var assignment = AutoFillColumnLayoutCalculator.DistributeColumns(cardCount, columnCount);

        // Assert
        Assert.Equal(cardCount, assignment.Length);
        Assert.Equal(expectedCounts, CountsFor(assignment, columnCount));
        Assert.True(IsNonDecreasing(assignment), "cards must fill a column before moving to the next");
    }

    [Theory]
    [MemberData(nameof(BalancedDistributionCases))]
    public void DistributeColumns_EveryColumnGetsAtLeastOneCard_WhenColumnsDoNotExceedCards(int cardCount, int columnCount)
    {
        // Act
        var assignment = AutoFillColumnLayoutCalculator.DistributeColumns(cardCount, columnCount);
        var counts = CountsFor(assignment, columnCount);

        // Assert
        Assert.All(counts, static count => Assert.True(count >= 1));
        Assert.True(counts.Max() - counts.Min() <= 1, "columns must be balanced to within one card");
        Assert.Equal(cardCount, counts.Sum());
    }

    public static IEnumerable<object[]> BalancedDistributionCases()
    {
        for (var cardCount = 1; cardCount <= 12; cardCount++)
        {
            for (var columnCount = 1; columnCount <= cardCount; columnCount++)
            {
                yield return [cardCount, columnCount];
            }
        }
    }

    [Theory]
    // Tall viewport: everything fits in one column, so widening must not add a column.
    [InlineData(850, 1000, 9, 100, 1, "9")]
    // Medium viewport: fits at three columns.
    [InlineData(850, 350, 9, 100, 3, "3,3,3")]
    // Low/wide rectangle, width caps at four columns and nothing fits: four columns, none empty (the regression).
    [InlineData(850, 250, 9, 100, 4, "3,2,2,2")]
    // Wider still allows five columns, which fit.
    [InlineData(1100, 250, 9, 100, 5, "2,2,2,2,1")]
    // Unknown viewport height (sentinel < 0): fall back to the width-derived column count.
    [InlineData(850, -1, 9, 100, 4, "3,2,2,2")]
    // Narrow width caps at one column even though the content overflows.
    [InlineData(150, 250, 9, 100, 1, "9")]
    public void Calculate_PicksFewestColumnsThatFit_WithNoEmptyColumns(
        double availableWidth,
        double viewportHeight,
        int cardCount,
        double cardHeight,
        int expectedColumnCount,
        string expectedCountsCsv)
    {
        // Arrange
        var input = Input(availableWidth, viewportHeight);
        var expectedCounts = ParseCounts(expectedCountsCsv);

        // Act
        var result = AutoFillColumnLayoutCalculator.Calculate(input, ConstantHeights(cardCount, cardHeight));

        // Assert
        Assert.Equal(expectedColumnCount, result.ColumnCount);
        Assert.Equal(expectedCounts, ColumnCounts(result));
        Assert.All(ColumnCounts(result), static count => Assert.True(count >= 1, "no column may be empty"));
        Assert.Equal(cardCount, result.Placements.Count);
    }

    [Fact]
    public void Calculate_StretchesColumnsToFillWidth_WhenContentFits()
    {
        // Arrange: tall viewport so a single column fits; the one column must span the full width.
        var input = Input(availableWidth: 850, viewportHeight: 1000);

        // Act
        var result = AutoFillColumnLayoutCalculator.Calculate(input, ConstantHeights(9, 100));

        // Assert
        Assert.Equal(1, result.ColumnCount);
        Assert.Equal(850, result.ColumnWidth, 3);
    }

    [Fact]
    public void Calculate_ClampsColumnsToCardCount_AndDividesWidthAcrossThem()
    {
        // Arrange: width fits four columns but there are only two cards and the viewport forces a split.
        var input = Input(availableWidth: 850, viewportHeight: 150);

        // Act
        var result = AutoFillColumnLayoutCalculator.Calculate(input, ConstantHeights(2, 100));

        // Assert
        Assert.Equal(2, result.ColumnCount);
        Assert.Equal(425, result.ColumnWidth, 3);
    }

    [Fact]
    public void Calculate_StacksCardsWithRowSpacing_WithinAColumn()
    {
        // Arrange: one column (width fits one), three cards, 10px row spacing.
        var input = new AutoFillColumnLayoutInput(
            AvailableWidth: 300,
            ViewportHeight: 1000,
            DesiredColumnWidth: 200,
            ColumnSpacing: 0,
            RowSpacing: 10);

        // Act
        var result = AutoFillColumnLayoutCalculator.Calculate(input, ConstantHeights(3, 100));

        // Assert
        Assert.Equal(1, result.ColumnCount);
        Assert.Equal(new[] { 0d, 110d, 220d }, result.Placements.Select(static p => p.Y).ToArray());
        Assert.Equal(320, result.Height, 3);
    }

    [Fact]
    public void Calculate_OffsetsColumnsByColumnSpacing()
    {
        // Arrange: spacing widens columns apart; force the max (width-derived) column count via a tiny viewport.
        var input = new AutoFillColumnLayoutInput(
            AvailableWidth: 800,
            ViewportHeight: 50,
            DesiredColumnWidth: 200,
            ColumnSpacing: 20,
            RowSpacing: 0);

        // Act
        var result = AutoFillColumnLayoutCalculator.Calculate(input, ConstantHeights(6, 100));

        // Assert: floor((800 + 20) / (200 + 20)) = 3 columns; width = (800 - 2*20) / 3.
        Assert.Equal(3, result.ColumnCount);
        var expectedColumnWidth = (800d - (2 * 20d)) / 3d;
        Assert.Equal(expectedColumnWidth, result.ColumnWidth, 3);
        var secondColumnCard = result.Placements.First(static p => p.Column == 1);
        Assert.Equal(expectedColumnWidth + 20d, secondColumnCard.X, 3);
    }

    [Fact]
    public void Calculate_MeasuresCardsAtTheChosenColumnWidth_Finally()
    {
        // Arrange: record every width the calculator measures at.
        var measuredWidths = new List<double>();
        var input = Input(availableWidth: 850, viewportHeight: 250);

        IReadOnlyList<double> Measure(double columnWidth)
        {
            measuredWidths.Add(columnWidth);
            return Enumerable.Repeat(100d, 9).ToList();
        }

        // Act
        var result = AutoFillColumnLayoutCalculator.Calculate(input, Measure);

        // Assert: the last measurement is at the column width the result reports.
        Assert.Equal(result.ColumnWidth, measuredWidths[^1], 3);
    }

    [Fact]
    public void Calculate_ReturnsEmptyLayout_WhenNoCardsOccupy()
    {
        // Arrange
        var input = Input(availableWidth: 850, viewportHeight: 250);

        // Act
        var result = AutoFillColumnLayoutCalculator.Calculate(input, static _ => Array.Empty<double>());

        // Assert
        Assert.Empty(result.Placements);
        Assert.Equal(0, result.Height, 3);
        Assert.Equal(1, result.ColumnCount);
    }

    private static AutoFillColumnLayoutInput Input(double availableWidth, double viewportHeight) =>
        new(
            AvailableWidth: availableWidth,
            ViewportHeight: viewportHeight < 0 ? double.PositiveInfinity : viewportHeight,
            DesiredColumnWidth: 200,
            ColumnSpacing: 0,
            RowSpacing: 0);

    private static Func<double, IReadOnlyList<double>> ConstantHeights(int cardCount, double height) =>
        _ => Enumerable.Repeat(height, cardCount).ToList();

    private static int[] ColumnCounts(AutoFillColumnLayoutResult result)
    {
        var counts = new int[result.ColumnCount];
        foreach (var placement in result.Placements)
        {
            counts[placement.Column]++;
        }

        return counts;
    }

    private static int[] CountsFor(int[] assignment, int columnCount)
    {
        var counts = new int[columnCount];
        foreach (var column in assignment)
        {
            counts[column]++;
        }

        return counts;
    }

    private static int[] ParseCounts(string csv) =>
        csv.Split(',').Select(static value => int.Parse(value)).ToArray();

    private static bool IsNonDecreasing(int[] values)
    {
        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] < values[i - 1])
            {
                return false;
            }
        }

        return true;
    }
}
