namespace WinUiFileManager.Presentation;

internal static class WinUiViewHelper
{
    public static bool IsModifierDown(VirtualKey key) =>
        InputKeyboardSource
            .GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    public static bool HasAnyModifier(params VirtualKey[] keys) =>
        keys.Any(IsModifierDown);

    public static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }
}
