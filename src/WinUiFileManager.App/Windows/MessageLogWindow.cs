using Microsoft.UI.Xaml;

namespace WinUiFileManager.App.Windows;

using Presentation.MessageLogging;

public sealed class MessageLogWindow : Window
{
    private static readonly List<MessageLogWindow> OpenInstances = [];

    public MessageLogWindow(MessageLogStore messageLogStore)
    {
        Title = "Message log";
        ExtendsContentIntoTitleBar = false;
        Content = new MessageLogView(messageLogStore);
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(960, 640));

        OpenInstances.Add(this);

        Closed += (_, _) => OpenInstances.Remove(this);
    }

    // WinUi has no concept of child windows, so we must manually close all instances on main window close
    public static void CloseAll() => OpenInstances.ToList().ForEach(static i => i.Close());
}
