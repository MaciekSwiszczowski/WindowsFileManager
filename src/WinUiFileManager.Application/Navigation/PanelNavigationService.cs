using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Application.Messages.RequestMessages.Navigation;

namespace WinUiFileManager.Application.Navigation;

#pragma warning disable MA0026
/// <summary>
/// Coordinates panel navigation requests and keeps the current path for each panel identity. It listens
/// for the navigation <c>*Requested</c> messages, resolves/validates the target directory, records it,
/// and then publishes the single <see cref="FileTableNavigateToPathMessage"/> that panels consume.
/// TODO:
/// - Replace synchronous path existence checks with an investigated non-blocking API.
/// - Decide user-facing error messages for missing paths, security errors, and admin-required navigation.
/// - Serve current panel paths for application-state serialization on close.
/// - Store volume identity so inactive panels can detect volume changes before reloading.
/// </summary>
/// <remarks>
/// Lifetime/registration hazards (see AGENTS.md §4–§5):
/// <list type="bullet">
/// <item><description>
/// <see cref="Initialize"/> performs deferred messenger registration but is <b>not idempotent</b> — calling
/// it twice double-registers the handlers and double-handles every navigation message. Call it exactly once.
/// </description></item>
/// <item><description>
/// <see cref="Dispose"/> unregisters from the strong-reference messenger, but it is only reached if the DI
/// container is disposed on shutdown. If the container is not disposed, this singleton stays rooted for the
/// process lifetime (a latent leak); that is acceptable here only because it lives for the whole app.
/// </description></item>
/// </list>
/// </remarks>
#pragma warning restore MA0026
public sealed class PanelNavigationService : IDisposable
{
    private readonly Dictionary<string, NormalizedPath> _currentPaths = new(StringComparer.Ordinal);
    private readonly IMessenger _messenger;
    private bool _disposed;

    public PanelNavigationService(IMessenger messenger)
    {
        _messenger = messenger;
    }

    /// <summary>
    /// Registers handlers for the navigation request messages. <b>Not idempotent</b> — must be called
    /// exactly once (see the lifetime note on the type); a second call double-registers the handlers.
    /// </summary>
    public void Initialize()
    {
        _messenger.Register<FileTableNavigateToPathRequestedMessage>(this, OnNavigateToPathRequested);
        _messenger.Register<FileTableNavigateUpRequestedMessage>(this, OnNavigateUpRequested);
        _messenger.Register<FileTableNavigateDownRequestedMessage>(this, OnNavigateDownRequested);
    }

    /// <summary>
    /// Unregisters from the messenger. Idempotent. Only effective if actually invoked (i.e. the DI
    /// container is disposed on shutdown); otherwise the registration outlives this call.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    private void OnNavigateToPathRequested(object recipient, FileTableNavigateToPathRequestedMessage message)
    {
        TryNavigate(message.Identity, message.Path);
    }

    private void OnNavigateUpRequested(object recipient, FileTableNavigateUpRequestedMessage message)
    {
        if (!_currentPaths.TryGetValue(message.Identity, out var currentPath))
        {
            return;
        }

        var parent = Directory.GetParent(currentPath.DisplayPath);
        if (parent is null)
        {
            return;
        }

        TryNavigate(message.Identity, NormalizedPath.FromUserInput(parent.FullName));
    }

    private void OnNavigateDownRequested(object recipient, FileTableNavigateDownRequestedMessage message)
    {
        if (!_currentPaths.TryGetValue(message.Identity, out var currentPath)
            || string.IsNullOrWhiteSpace(message.FolderName))
        {
            return;
        }

        var childPath = new NormalizedPath(Path.Combine(currentPath.Value, message.FolderName));
        TryNavigate(message.Identity, childPath);
    }

    private void TryNavigate(string identity, NormalizedPath path)
    {
        // Synchronous existence check (a known TODO above): silently no-ops on a missing/inaccessible
        // directory rather than surfacing an error. Only valid targets update state and notify panels.
        if (!Directory.Exists(path.DisplayPath))
        {
            return;
        }

        _currentPaths[identity] = path;
        _messenger.Send(new FileTableNavigateToPathMessage(identity, path));
    }
}
