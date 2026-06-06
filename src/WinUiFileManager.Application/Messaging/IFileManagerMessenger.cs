using CommunityToolkit.Mvvm.Messaging;
using R3;

namespace WinUiFileManager.Application.Messaging;

/// <summary>
/// Application messenger abstraction that extends CommunityToolkit's <see cref="IMessenger"/> with
/// app-specific registration helpers.
/// </summary>
/// <remarks>
/// Consumers should depend on this interface when they need identity-aware registration, optional
/// UI-thread dispatch, or observable conversion. Consumers that only send or register plain CommunityToolkit
/// messages can continue depending on <see cref="IMessenger"/>. Implementations still use strong-reference
/// messenger semantics, so recipients must unregister during teardown.
/// </remarks>
public interface IFileManagerMessenger : IMessenger
{
    /// <summary>
    /// Registers a token-scoped handler with optional app-specific registration behavior.
    /// </summary>
    /// <typeparam name="TRecipient">The recipient type rooted by the underlying messenger.</typeparam>
    /// <typeparam name="TMessage">The message type to handle.</typeparam>
    /// <typeparam name="TToken">The messenger token type.</typeparam>
    /// <param name="recipient">The recipient instance that owns the registration and must unregister during teardown.</param>
    /// <param name="token">The token identifying the message channel.</param>
    /// <param name="handler">The handler invoked for matching messages.</param>
    /// <param name="options">Optional registration behavior.</param>
    void Register<TRecipient, TMessage, TToken>(
        TRecipient recipient,
        TToken token,
        MessageHandler<TRecipient, TMessage> handler,
        Options options)
        where TRecipient : class
        where TMessage : class
        where TToken : IEquatable<TToken>;

    /// <summary>
    /// Registers a default-channel handler with optional app-specific registration behavior.
    /// </summary>
    /// <typeparam name="TRecipient">The recipient type rooted by the underlying messenger.</typeparam>
    /// <typeparam name="TMessage">The message type to handle.</typeparam>
    /// <param name="recipient">The recipient instance that owns the registration and must unregister during teardown.</param>
    /// <param name="handler">The handler invoked for matching messages.</param>
    /// <param name="options">Optional registration behavior.</param>
    void Register<TRecipient, TMessage>(
        TRecipient recipient,
        MessageHandler<TRecipient, TMessage> handler,
        Options options)
        where TRecipient : class
        where TMessage : class;

    /// <summary>
    /// Registers a default-channel handler for messages matching a specific application identity.
    /// </summary>
    /// <typeparam name="TRecipient">The recipient type rooted by the underlying messenger.</typeparam>
    /// <typeparam name="TMessage">The identity-bearing message type to handle.</typeparam>
    /// <param name="recipient">The recipient instance that owns the registration and must unregister during teardown.</param>
    /// <param name="identity">The identity value that incoming messages must match.</param>
    /// <param name="handler">The handler invoked for matching messages.</param>
    /// <param name="options">Optional registration behavior.</param>
    void Register<TRecipient, TMessage>(
        TRecipient recipient,
        Identity identity,
        MessageHandler<TRecipient, TMessage> handler,
        Options options = Options.None)
        where TRecipient : class
        where TMessage : class, IIdentityMessage;

    /// <summary>
    /// Registers a default-channel identity-filtered handler when the handler does not need the recipient instance.
    /// </summary>
    /// <typeparam name="TMessage">The identity-bearing message type to handle.</typeparam>
    /// <param name="recipient">The recipient instance that owns the registration and must unregister during teardown.</param>
    /// <param name="identity">The identity value that incoming messages must match.</param>
    /// <param name="handler">The handler invoked for matching messages.</param>
    /// <param name="options">Optional registration behavior.</param>
    void Register<TMessage>(
        object recipient,
        Identity identity,
        Action<TMessage> handler,
        Options options = Options.None)
        where TMessage : class, IIdentityMessage;

    /// <summary>
    /// Creates a cold R3 observable sequence backed by a default-channel messenger registration.
    /// </summary>
    /// <typeparam name="TMessage">The message type emitted by the observable.</typeparam>
    /// <returns>A cold observable. Each subscription creates and owns its messenger registration.</returns>
    Observable<TMessage> CreateObservable<TMessage>()
        where TMessage : class;

    /// <summary>
    /// Creates a cold R3 observable sequence backed by a token-scoped messenger registration.
    /// </summary>
    /// <typeparam name="TMessage">The message type emitted by the observable.</typeparam>
    /// <typeparam name="TToken">The messenger token type.</typeparam>
    /// <param name="token">The token identifying the message channel observed by each subscription.</param>
    /// <returns>A cold observable. Each subscription creates and owns its messenger registration.</returns>
    Observable<TMessage> CreateObservable<TMessage, TToken>(TToken token)
        where TMessage : class
        where TToken : IEquatable<TToken>;
}
