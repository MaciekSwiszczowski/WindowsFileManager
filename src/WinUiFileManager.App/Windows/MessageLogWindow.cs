using Microsoft.UI.Xaml;

namespace WinUiFileManager.App.Windows;

using Presentation.MessageLogging;

public sealed class MessageLogWindow : Window
{
    public MessageLogWindow(MessageLogStore messageLogStore)
    {
        Title = "Message log";
        ExtendsContentIntoTitleBar = false;
        Content = new MessageLogView(messageLogStore);
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(960, 640));
    }
}
