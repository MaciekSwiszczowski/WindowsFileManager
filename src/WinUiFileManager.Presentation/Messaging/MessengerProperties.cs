namespace WinUiFileManager.Presentation.Messaging;

public static class MessengerProperties
{
    public static readonly DependencyProperty MessengerProperty =
        DependencyProperty.RegisterAttached(
            "Messenger",
            typeof(IMessenger),
            typeof(MessengerProperties),
            new PropertyMetadata(defaultValue: null));

    public static IMessenger? GetMessenger(DependencyObject obj) => (IMessenger?)obj.GetValue(MessengerProperty);

    public static void SetMessenger(DependencyObject obj, IMessenger? value) => obj.SetValue(MessengerProperty, value);
}
