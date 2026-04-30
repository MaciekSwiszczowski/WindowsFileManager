namespace WinUiFileManager.Presentation.FileEntryTable;

internal static class FileEntryTableVisualTreeExtensions
{
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

    public static SpecFileEntryViewModel? FindItem(this DependencyObject? source)
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

    public static T? FindAncestor<T>(this DependencyObject? source)
        where T : DependencyObject
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

    public static T? FindDescendant<T>(this DependencyObject? source, Func<T, bool> predicate)
        where T : DependencyObject
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
