using Microsoft.UI.Xaml;

namespace WinUiFileManager.App.Windows;

using Presentation.MessageLogging;

/// <summary>
/// Standalone diagnostic window that hosts a <see cref="MessageLogView"/> over a shared
/// <see cref="MessageLogStore"/>. App layer; a development aid (opened only from Debug builds).
/// </summary>
/// <remarks>
/// Because WinUI has no parent/child window relationship, open instances are tracked in a static list so
/// they can all be force-closed when the main window closes. Each instance removes itself from that list
/// on <c>Closed</c>, so the static list does not leak windows. All members are UI-thread affine.
/// </remarks>
public sealed class MessageLogWindow : Window
{
    private static readonly List<MessageLogWindow> OpenInstances = [];

    /// <summary>
    /// Creates and sizes a log window bound to the given store, and registers it for global close.
    /// </summary>
    /// <param name="messageLogStore">The shared, app-wide message log to display (not owned by this window).</param>
    public MessageLogWindow(MessageLogStore messageLogStore)
    {
        Title = "Message log";
        ExtendsContentIntoTitleBar = false;
        Content = new MessageLogView(messageLogStore);
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(960, 640));

        OpenInstances.Add(this);

        // Self-deregister on close so the static tracking list never holds dead windows.
        Closed += (_, _) => OpenInstances.Remove(this);
    }

    /// <summary>Closes every open log window. Called when the main shell window shuts down.</summary>
    // WinUi has no concept of child windows, so we must manually close all instances on main window close.
    // Snapshot via ToList() because each Close() mutates OpenInstances through the Closed handler.
    public static void CloseAll() => OpenInstances.ToList().ForEach(static i => i.Close());
}
