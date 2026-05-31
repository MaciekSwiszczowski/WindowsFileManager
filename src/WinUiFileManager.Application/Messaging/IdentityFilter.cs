using CommunityToolkit.Mvvm.Messaging;

namespace WinUiFileManager.Application.Messaging;

/// <summary>
/// Builds messenger handlers that only fire for messages matching a given pane <see cref="Identity"/>.
/// </summary>
/// <remarks>
/// Pane-scoped behaviors must register through this so a left-pane recipient never reacts to
/// right-pane messages (see AGENTS.md §4). Note the equality check relies on
/// <see cref="Identity"/>'s value equality and its implicit string conversions.
/// </remarks>
public static class IdentityFilter
{
    /// <summary>
    /// Wraps <paramref name="handler"/> in a messenger handler that invokes it only when the incoming
    /// message's <see cref="IIdentityMessage.Identity"/> equals <paramref name="identity"/>.
    /// </summary>
    /// <typeparam name="TMessage">The identity-scoped message type to handle.</typeparam>
    /// <param name="identity">The pane identity this handler is bound to.</param>
    /// <param name="handler">The callback to run for matching messages.</param>
    /// <returns>A <see cref="MessageHandler{TRecipient,TMessage}"/> suitable for <c>IMessenger.Register</c>.</returns>
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
