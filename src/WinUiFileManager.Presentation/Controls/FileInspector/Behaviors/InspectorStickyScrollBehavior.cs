using WinUiFileManager.Presentation.Controls.FileInspector.Panel;

namespace WinUiFileManager.Presentation.Controls.FileInspector.Behaviors;

/// <summary>
/// View behavior for <see cref="InspectorSingleSelectionView"/> that makes it follow the bottom: once the user
/// scrolls to the bottom, the inspector stays pinned to the bottom across every content height change — switching
/// files, reloads, and deferred-diagnostic fills — so a bottom property can be watched while navigating. Scrolling
/// up turns it off; a user reading mid-list is never yanked.
/// </summary>
/// <remarks>
/// Pure view-element coordination (ScrollViewer offset / <c>ChangeView</c>), which cannot live on a view model.
/// Wiring is deferred to <see cref="FrameworkElement.Loaded"/> (the named elements are not available at attach time)
/// and reversed on <see cref="FrameworkElement.Unloaded"/> and detach, so subscriptions stay balanced. Card
/// collapse/expand re-layout is handled by <see cref="AutoFillColumnsPanel"/> itself, so this behavior owns only the
/// scroll position.
/// </remarks>
public sealed class InspectorStickyScrollBehavior : Behavior<InspectorSingleSelectionView>
{
    // Pixel tolerance for treating the scroll position as "at the bottom" (guards against fractional offsets).
    private const double BottomEpsilon = 1.0;

    // The "follow the bottom" latch: set while the user is parked at the bottom of scrollable content, cleared when
    // they scroll up. While set, every content height change re-pins to the bottom. Persists through transient
    // non-scrollable reflow frames (see OnScrollViewChanged) so it survives a file switch.
    private bool _stickToBottom;
    private bool _wired;
    private ScrollViewer? _scrollViewer;
    private FrameworkElement? _content;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnViewLoaded;
        AssociatedObject.Unloaded += OnViewUnloaded;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnViewLoaded;
        AssociatedObject.Unloaded -= OnViewUnloaded;
        Unwire();
        base.OnDetaching();
    }

    private void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded can fire more than once (re-parenting); wire exactly once until the next Unloaded.
        if (_wired)
        {
            return;
        }

        _scrollViewer = AssociatedObject.ScrollHost;
        _content = AssociatedObject.CategoriesHost;

        _scrollViewer.ViewChanged += OnScrollViewChanged;
        _content.SizeChanged += OnContentSizeChanged;

        _wired = true;
    }

    private void OnViewUnloaded(object sender, RoutedEventArgs e)
    {
        Unwire();
    }

    private void Unwire()
    {
        if (!_wired || _scrollViewer is null || _content is null)
        {
            return;
        }

        _scrollViewer.ViewChanged -= OnScrollViewChanged;
        _content.SizeChanged -= OnContentSizeChanged;

        _wired = false;
    }

    private void OnScrollViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        // Ignore intermediate drag/fling frames; only settled positions change the latch.
        if (e.IsIntermediate || _scrollViewer is not { } scrollViewer)
        {
            return;
        }

        // Only update the latch on genuinely scrollable content. While switching files the new item's fields are
        // briefly cleared, leaving the content non-scrollable for a frame; treating that as "not at the bottom"
        // would clear the latch and stop us following the bottom as the new content streams in.
        if (scrollViewer.ScrollableHeight <= BottomEpsilon)
        {
            return;
        }

        _stickToBottom = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - BottomEpsilon;
    }

    // While the latch is set, follow every reflow: each height change re-pins to the (new) bottom.
    private void OnContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_stickToBottom && _scrollViewer is { } scrollViewer)
        {
            scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null, disableAnimation: true);
        }
    }
}
