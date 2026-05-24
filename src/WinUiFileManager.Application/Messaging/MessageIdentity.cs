using CommunityToolkit.Mvvm.Messaging;

namespace WinUiFileManager.Application.Messaging;

public static class MessageIdentity
{
    public static MessageHandler<object, TMessage> Filter<TMessage>(
        Identity identity,
        Action<TMessage> handler)
        where TMessage : class, IIdentityMessage
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Value);
        ArgumentNullException.ThrowIfNull(handler);

        return (_, message) =>
        {
            if (message.Identity == identity)
            {
                handler(message);
            }
        };
    }
}
