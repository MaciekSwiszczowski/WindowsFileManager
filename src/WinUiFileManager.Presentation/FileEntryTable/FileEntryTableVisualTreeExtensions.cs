namespace WinUiFileManager.Presentation.FileEntryTable;

/// <summary>
/// Visual-tree and pointer helpers used by the file-table behaviors to translate raw pointer events
/// and visual-tree nodes into the row view model / control they care about.
/// </summary>
internal static class FileEntryTableVisualTreeExtensions
{
    /// <summary>
    /// True when <paramref name="e"/> represents the "primary" press for its device: left mouse/pen
    /// button down, or any touch contact. Lets behaviors treat a left-click and a tap uniformly while
    /// ignoring right/middle button presses.
    /// </summary>
    public static bool IsPrimaryPointerPress(this PointerRoutedEventArgs e)
    {
        var props = e.GetCurrentPoint(null).Properties;
        return e.Pointer.PointerDeviceType switch
        {
            PointerDeviceType.Mouse or PointerDeviceType.Pen =>
                props.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed,
            PointerDeviceType.Touch => true,
            _ => false,
        };
    }

    extension(DependencyObject? source)
    {
        /// <summary>Walks up the visual tree from this node and returns the first
        /// <see cref="SpecFileEntryViewModel"/> found as a <see cref="FrameworkElement.DataContext"/>,
        /// i.e. the row the event originated in; null if the node is not inside a row.</summary>
        public SpecFileEntryViewModel? FindItem()
        {
            for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is FrameworkElement { DataContext: SpecFileEntryViewModel item })
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>Walks up the visual tree and returns the nearest ancestor of type
        /// <typeparamref name="T"/> (including this node), or null if none.</summary>
        public T? FindAncestor<T>() where T : DependencyObject
        {
            for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is T match)
                {
                    return match;
                }
            }

            return null;
        }

        /// <summary>Depth-first searches the visual subtree for the first descendant of type
        /// <typeparamref name="T"/>.</summary>
        public T? FindDescendant<T>() where T : DependencyObject
            => source.FindDescendant<T>(static _ => true);

        /// <summary>Depth-first searches the visual subtree for the first descendant of type
        /// <typeparamref name="T"/> matching <paramref name="predicate"/>; null if none.</summary>
        public T? FindDescendant<T>(Func<T, bool> predicate) where T : DependencyObject
        {
            if (source is null)
            {
                return null;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(source);
            for (var i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(source, i);
                if (child is T match && predicate(match))
                {
                    return match;
                }

                if (child.FindDescendant(predicate) is { } descendant)
                {
                    return descendant;
                }
            }

            return null;
        }
    }

}
