using Windows.Foundation;

namespace WinUiFileManager.Presentation.Controls.FileInspector.Panel;

/// <summary>
/// A non-virtualizing panel that lays children out in responsive, order-preserving, independently-packed columns.
/// Cards fill column-major in declaration order and every card is stretched to a uniform column width; each column is a
/// tight, top-aligned vertical stack, so a short card never leaves whitespace beside a taller card in another column.
/// The column-fill math lives in <see cref="AutoFillColumnLayoutCalculator"/>; this panel only supplies measurement and
/// applies the result.
/// </summary>
/// <remarks>
/// <para>
/// Chosen over <c>StaggeredLayout</c> for the inspector because masonry placement is height-driven, so a single long
/// value reshuffles which column every category lands in, which disorients the user. See
/// <see cref="AutoFillColumnLayoutCalculator"/> for the two layout rules (fewest columns that fit the viewport height;
/// balanced contiguous fill with no empty columns).
/// </para>
/// <para>
/// The panel sits in a vertically-scrolling <see cref="ScrollViewer"/>, which measures it with infinite height, so it
/// cannot learn the viewport height from the measure constraint; it instead discovers its ancestor
/// <see cref="ScrollViewer"/> on load and reads <see cref="ScrollViewer.ViewportHeight"/>, re-measuring when that height
/// changes. With no scroll-viewport ancestor (or before its height is known) the calculator falls back to the
/// width-derived column count.
/// </para>
/// <para>
/// Non-virtualizing is intentional and fine here: the inspector has a small, fixed set of category cards, not the large
/// virtualized row set the file table needs.
/// </para>
/// </remarks>
public sealed class AutoFillColumnsPanel : Microsoft.UI.Xaml.Controls.Panel
{
    // Pixel tolerance for treating a viewport height change as significant enough to re-measure.
    private const double ViewportEpsilon = 0.5;

    /// <summary>Target column width; the column count is at most the number of these that fit the available width.</summary>
    public static readonly DependencyProperty DesiredColumnWidthProperty = DependencyProperty.Register(
        nameof(DesiredColumnWidth),
        typeof(double),
        typeof(AutoFillColumnsPanel),
        new PropertyMetadata(260.0, OnLayoutPropertyChanged));

    /// <summary>Horizontal gap between columns.</summary>
    public static readonly DependencyProperty ColumnSpacingProperty = DependencyProperty.Register(
        nameof(ColumnSpacing),
        typeof(double),
        typeof(AutoFillColumnsPanel),
        new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    /// <summary>Vertical gap between cards stacked within a column.</summary>
    public static readonly DependencyProperty RowSpacingProperty = DependencyProperty.Register(
        nameof(RowSpacing),
        typeof(double),
        typeof(AutoFillColumnsPanel),
        new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    // Cached so a delegate is not allocated on every measure pass.
    private readonly Func<double, IReadOnlyList<double>> _measureCardHeights;

    // Ancestor scroll viewport, discovered on load; its ViewportHeight is the height the column count must fit within.
    private ScrollViewer? _scrollViewer;

    // Last known viewport height; PositiveInfinity means "unknown" (no scroll ancestor yet), which makes the calculator
    // fall back to the width-derived column count.
    private double _viewportHeight = double.PositiveInfinity;

    // Layout decision captured in MeasureOverride and applied in ArrangeOverride.
    private AutoFillColumnLayoutResult? _layout;

    /// <summary>
    /// Wires the measure callback and subscribes to load/unload so the panel can find and release its ancestor
    /// <see cref="ScrollViewer"/>, whose viewport height drives how many columns are needed before the content scrolls.
    /// </summary>
    public AutoFillColumnsPanel()
    {
        _measureCardHeights = MeasureOccupyingCardHeights;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <inheritdoc cref="DesiredColumnWidthProperty"/>
    public double DesiredColumnWidth
    {
        get => (double)GetValue(DesiredColumnWidthProperty);
        set => SetValue(DesiredColumnWidthProperty, value);
    }

    /// <inheritdoc cref="ColumnSpacingProperty"/>
    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <inheritdoc cref="RowSpacingProperty"/>
    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var input = new AutoFillColumnLayoutInput(
            availableSize.Width,
            _viewportHeight,
            DesiredColumnWidth,
            ColumnSpacing,
            RowSpacing);

        _layout = AutoFillColumnLayoutCalculator.Calculate(input, _measureCardHeights);
        return new Size(_layout.Width, _layout.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var placements = _layout?.Placements ?? Array.Empty<AutoFillCardPlacement>();
        var placementIndex = 0;
        foreach (var child in Children)
        {
            if (!IsOccupying(child) || placementIndex >= placements.Count)
            {
                // Park non-occupying (collapsed/filtered) children, and any child without a placement, off-layout so
                // they consume no slot.
                child.Arrange(new Rect(0, 0, 0, 0));
                if (IsOccupying(child))
                {
                    placementIndex++;
                }

                continue;
            }

            var placement = placements[placementIndex];
            child.Arrange(new Rect(placement.X, placement.Y, placement.Width, placement.Height));
            placementIndex++;
        }

        return finalSize;
    }

    // Measures every child at the given column width and returns the heights of those that occupy a slot, in order.
    private IReadOnlyList<double> MeasureOccupyingCardHeights(double columnWidth)
    {
        var constraint = new Size(columnWidth, double.PositiveInfinity);
        var heights = new List<double>();
        foreach (var child in Children)
        {
            child.Measure(constraint);
            if (IsOccupying(child))
            {
                heights.Add(child.DesiredSize.Height);
            }
        }

        return heights;
    }

    // A child occupies a column slot only when it is shown and has real height; a collapsed/empty card (e.g. filtered
    // out by search) measures to zero and is skipped so it leaves no hole in the flow.
    private static bool IsOccupying(UIElement child) =>
        child is { Visibility: Visibility.Visible, DesiredSize.Height: > 0 };

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer is not null)
        {
            return;
        }

        _scrollViewer = FindAncestorScrollViewer();
        if (_scrollViewer is not null)
        {
            _scrollViewer.SizeChanged += OnScrollViewerSizeChanged;
            UpdateViewportHeight();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer is not null)
        {
            _scrollViewer.SizeChanged -= OnScrollViewerSizeChanged;
            _scrollViewer = null;
        }

        _viewportHeight = double.PositiveInfinity;
    }

    private void OnScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (UpdateViewportHeight())
        {
            InvalidateMeasure();
        }
    }

    // Reads the current viewport height; returns true (and stores it) only when it is a usable, changed value, so a
    // pre-layout zero height or a no-op resize does not trigger a redundant re-measure.
    private bool UpdateViewportHeight()
    {
        if (_scrollViewer is null)
        {
            return false;
        }

        var viewportHeight = _scrollViewer.ViewportHeight;
        if (double.IsNaN(viewportHeight) || viewportHeight <= 0 || Math.Abs(viewportHeight - _viewportHeight) < ViewportEpsilon)
        {
            return false;
        }

        _viewportHeight = viewportHeight;
        return true;
    }

    private ScrollViewer? FindAncestorScrollViewer()
    {
        DependencyObject current = this;
        while (VisualTreeHelper.GetParent(current) is { } parent)
        {
            if (parent is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            current = parent;
        }

        return null;
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutoFillColumnsPanel panel)
        {
            panel.InvalidateMeasure();
        }
    }
}
