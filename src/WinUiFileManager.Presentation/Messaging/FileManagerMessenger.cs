using R3;

namespace WinUiFileManager.Presentation.Messaging;

/// <summary>
/// Application-facing wrapper around <see cref="StrongReferenceMessenger"/> that keeps messenger-specific
/// conveniences in one place for the WinUI composition root.
/// </summary>
/// <remarks>
/// The underlying messenger uses strong references, so recipients are rooted until they call
/// <see cref="UnregisterAll(object)"/> or otherwise unregister their handlers. The UI-dispatch option is intended
/// only for fire-and-forget notifications; request/response messages should not use it because dispatching makes
/// the handler asynchronous relative to the original send operation.
/// </remarks>
public sealed class FileManagerMessenger : IFileManagerMessenger
{
    private readonly IMessenger _innerMessenger;
    private readonly IUiThreadDispatcher? _uiDispatcher;

    /// <summary>
    /// Initializes a new messenger wrapper using the process-wide strong-reference messenger and optional UI dispatcher.
    /// </summary>
    /// <param name="innerMessenger">The CommunityToolkit messenger instance that stores registrations and sends messages.</param>
    /// <param name="uiDispatcher">Optional UI dispatcher used by registrations that request UI-thread delivery.</param>
    public FileManagerMessenger(StrongReferenceMessenger innerMessenger, IUiThreadDispatcher? uiDispatcher = null)
    {
        _innerMessenger = innerMessenger;
        _uiDispatcher = uiDispatcher;
    }

    /// <inheritdoc />
    public void Cleanup() => _innerMessenger.Cleanup();

    /// <inheritdoc />
    public bool IsRegistered<TMessage, TToken>(object recipient, TToken token)
        where TMessage : class
        where TToken : IEquatable<TToken>
    {
        return _innerMessenger.IsRegistered<TMessage, TToken>(recipient, token);
    }

    /// <inheritdoc />
    public void Register<TRecipient, TMessage, TToken>(
        TRecipient recipient,
        TToken token,
        MessageHandler<TRecipient, TMessage> handler)
        where TRecipient : class
        where TMessage : class
        where TToken : IEquatable<TToken>
    {
        _innerMessenger.Register(recipient, token, handler);
    }

    /// <summary>
    /// Registers a message handler and optionally dispatches handler execution to the UI thread.
    /// </summary>
    /// <typeparam name="TRecipient">The recipient type rooted by the underlying strong-reference messenger.</typeparam>
    /// <typeparam name="TMessage">The message type to handle.</typeparam>
    /// <typeparam name="TToken">The messenger token type.</typeparam>
    /// <param name="recipient">The recipient instance that owns the registration and must unregister during teardown.</param>
    /// <param name="token">The token identifying the message channel.</param>
    /// <param name="handler">The handler invoked for matching messages.</param>
    /// <param name="options">Optional registration behaviors applied by this wrapper.</param>
    public void Register<TRecipient, TMessage, TToken>(
        TRecipient recipient,
        TToken token,
        MessageHandler<TRecipient, TMessage> handler,
        Options options)
        where TRecipient : class
        where TMessage : class
        where TToken : IEquatable<TToken>
    {
        _innerMessenger.Register<TRecipient, TMessage, TToken>(
            recipient,
            token,
            (registeredRecipient, message) => InvokeHandler(registeredRecipient, message, handler, options));
    }

    /// <summary>
    /// Registers a default-channel message handler and optionally dispatches handler execution to the UI thread.
    /// </summary>
    /// <typeparam name="TRecipient">The recipient type rooted by the underlying strong-reference messenger.</typeparam>
    /// <typeparam name="TMessage">The message type to handle.</typeparam>
    /// <param name="recipient">The recipient instance that owns the registration and must unregister during teardown.</param>
    /// <param name="handler">The handler invoked for matching messages.</param>
    /// <param name="options">Optional registration behaviors applied by this wrapper.</param>
    public void Register<TRecipient, TMessage>(
        TRecipient recipient,
        MessageHandler<TRecipient, TMessage> handler,
        Options options)
        where TRecipient : class
        where TMessage : class
    {
        this.Register<TRecipient, TMessage>(recipient,
            (registeredRecipient, message) => InvokeHandler(registeredRecipient, message, handler, options));
    }

    /// <summary>
    /// Registers an identity-filtered handler for messages scoped to a pane or another application identity.
    /// </summary>
    /// <typeparam name="TRecipient">The recipient type rooted by the underlying strong-reference messenger.</typeparam>
    /// <typeparam name="TMessage">The identity-bearing message type to handle.</typeparam>
    /// <param name="recipient">The recipient instance that owns the registration and must unregister during teardown.</param>
    /// <param name="identity">The identity value that incoming messages must match.</param>
    /// <param name="handler">The handler invoked for matching messages.</param>
    /// <param name="options">Optional registration behaviors applied by this wrapper.</param>
    public void Register<TRecipient, TMessage>(
        TRecipient recipient,
        Identity identity,
        MessageHandler<TRecipient, TMessage> handler,
        Options options = Options.None)
        where TRecipient : class
        where TMessage : class, IIdentityMessage
    {
        Register<TRecipient, TMessage>(
            recipient,
            (registeredRecipient, message) =>
            {
                if (message.Identity != identity)
                {
                    return;
                }

                handler(registeredRecipient, message);
            },
            options);
    }

    /// <summary>
    /// Registers an identity-filtered handler when the handler does not need the recipient instance.
    /// </summary>
    /// <typeparam name="TMessage">The identity-bearing message type to handle.</typeparam>
    /// <param name="recipient">The recipient instance that owns the registration and must unregister during teardown.</param>
    /// <param name="identity">The identity value that incoming messages must match.</param>
    /// <param name="handler">The handler invoked for matching messages.</param>
    /// <param name="options">Optional registration behaviors applied by this wrapper.</param>
    public void Register<TMessage>(
        object recipient,
        Identity identity,
        Action<TMessage> handler,
        Options options = Options.None)
        where TMessage : class, IIdentityMessage
    {
        Register<object, TMessage>(
            recipient,
            identity,
            (_, message) => handler(message),
            options);
    }

    /// <inheritdoc />
    public void Reset()
    {
        _innerMessenger.Reset();
    }

    /// <inheritdoc />
    public TMessage Send<TMessage, TToken>(TMessage message, TToken token)
        where TMessage : class
        where TToken : IEquatable<TToken>
    {
        return _innerMessenger.Send(message, token);
    }

    /// <inheritdoc />
    public void Unregister<TMessage, TToken>(object recipient, TToken token)
        where TMessage : class
        where TToken : IEquatable<TToken>
    {
        _innerMessenger.Unregister<TMessage, TToken>(recipient, token);
    }

    /// <inheritdoc />
    public void UnregisterAll(object recipient)
    {
        _innerMessenger.UnregisterAll(recipient);
    }

    /// <inheritdoc />
    public void UnregisterAll<TToken>(object recipient, TToken token)
        where TToken : IEquatable<TToken>
    {
        _innerMessenger.UnregisterAll(recipient, token);
    }

    /// <summary>
    /// Creates a cold R3 observable sequence backed by a default-channel messenger registration.
    /// </summary>
    /// <typeparam name="TMessage">The message type emitted by the observable.</typeparam>
    /// <returns>
    /// A cold observable. Each subscription creates its own messenger registration and unregisters it on disposal.
    /// </returns>
    public Observable<TMessage> CreateObservable<TMessage>() where TMessage : class
    {
        return Observable.Create<TMessage, FileManagerMessenger>(this, static (observer, vm) =>
        {
            var recipient = new R3ObservableRecipient<TMessage>(observer);

            vm.Register<R3ObservableRecipient<TMessage>, TMessage>(
                recipient,
                static (recipient, message) => recipient.Observer.OnNext(message));

            return Disposable.Create(
                (Messenger: vm, Recipient: recipient),
                static state => state.Messenger.UnregisterAll(state.Recipient));
        });
    }

    /// <summary>
    /// Creates a cold R3 observable sequence backed by a token-scoped messenger registration.
    /// </summary>
    /// <typeparam name="TMessage">The message type emitted by the observable.</typeparam>
    /// <typeparam name="TToken">The messenger token type.</typeparam>
    /// <param name="token">The token identifying the message channel observed by each subscription.</param>
    /// <returns>
    /// A cold observable. Each subscription creates its own messenger registration and unregisters it on disposal.
    /// </returns>
    public Observable<TMessage> CreateObservable<TMessage, TToken>(TToken token)
        where TMessage : class
        where TToken : IEquatable<TToken>
    {
        return Observable.Create<TMessage, (FileManagerMessenger Messenger, TToken Token)>(
            (this, token),
            static (observer, vm) =>
            {
                var recipient = new R3ObservableRecipient<TMessage>(observer);

                vm.Messenger.Register<R3ObservableRecipient<TMessage>, TMessage, TToken>(
                    recipient,
                    vm.Token,
                    static (recipient, message) => recipient.Observer.OnNext(message));

                return Disposable.Create(
                    (vm.Messenger, Recipient: recipient),
                    static state => state.Messenger.UnregisterAll(state.Recipient));
            });
    }

    /// <summary>Messenger recipient that forwards delivered messages to one R3 observer.</summary>
    private sealed class R3ObservableRecipient<TMessage>
        where TMessage : class
    {
        public R3ObservableRecipient(Observer<TMessage> observer)
        {
            Observer = observer;
        }

        public Observer<TMessage> Observer { get; }
    }

    private void InvokeHandler<TRecipient, TMessage>(
        TRecipient recipient,
        TMessage message,
        MessageHandler<TRecipient, TMessage> handler,
        Options options)
        where TRecipient : class
        where TMessage : class
    {
        if (options == Options.None)
        {
            handler(recipient, message);
            return;
        }

        if (options != Options.DispatchToUiThread)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options, "Unknown messenger registration option.");
        }

        if (_uiDispatcher is not { } uiDispatcher)
        {
            throw new InvalidOperationException("UI-dispatched messenger registrations require a UI dispatcher.");
        }

        if (uiDispatcher.HasThreadAccess)
        {
            handler(recipient, message);
            return;
        }

        uiDispatcher.Post(() => handler(recipient, message));
    }
}
