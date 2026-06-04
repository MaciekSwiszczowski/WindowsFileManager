namespace WinUiFileManager.Application.Messaging;

/// <summary>
/// Marker interface for every message published on the app-wide <c>IMessenger</c>. Gives the messaging
/// surface a single, searchable root type and a place to constrain generic messaging helpers (see AGENTS.md §4).
/// The App composition root exposes a wrapper over the process-wide strong-reference messenger, so recipients
/// still must unregister during teardown.
/// </summary>
public interface IFileManagerMessengerMessage;

/// <summary>
/// A <see cref="IFileManagerMessengerMessage"/> that carries a pane <see cref="Identity"/> so it can
/// be routed to a specific pane by the messenger wrapper's identity-aware registration methods.
/// </summary>
public interface IIdentityMessage : IFileManagerMessengerMessage
{
    /// <summary>The pane/scope this message targets.</summary>
    public Identity Identity { get; }
}
