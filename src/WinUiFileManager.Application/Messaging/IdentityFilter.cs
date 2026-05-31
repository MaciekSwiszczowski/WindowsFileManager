using CommunityToolkit.Mvvm.Messaging;

namespace WinUiFileManager.Application.Messaging;

public static class IdentityFilter
{
    public static MessageHandler<object, TMessage> For<TMessage>(Identity identity, Action<TMessage> handler) where TMessage : class, IIdentityMessage
    {
        return (_, message) =>
        {
            if (message.Identity == identity)
            {
                handler(message);
            }
        };
    }
}
