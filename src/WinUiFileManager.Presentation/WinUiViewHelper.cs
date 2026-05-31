namespace WinUiFileManager.Presentation;

/// <summary>
/// Small shared WinUI helpers used across views and behaviors: querying current modifier-key state and
/// a recursive visual-tree descendant search.
/// </summary>
internal static class WinUiViewHelper
{
    /// <summary>True when the given (modifier) key is currently down on the calling UI thread.</summary>
    public static bool IsModifierDown(VirtualKey key) =>
        InputKeyboardSource
            .GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    /// <summary>True when any of the given modifier keys is currently down.</summary>
    public static bool HasAnyModifier(params VirtualKey[] keys) =>
        keys.Any(IsModifierDown);

    /// <summary>Depth-first searches <paramref name="parent"/>'s visual subtree for the first descendant
    /// of type <typeparamref name="T"/>; null if none is found.</summary>
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
