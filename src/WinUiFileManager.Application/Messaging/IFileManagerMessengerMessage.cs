namespace WinUiFileManager.Application.Messaging;

/// <summary>
/// Marker interface for every message published on the app-wide <c>IMessenger</c>
/// (<c>StrongReferenceMessenger.Default</c>). Gives the messaging surface a single, searchable
/// root type and a place to constrain generic messaging helpers (see AGENTS.md §4).
/// </summary>
public interface IFileManagerMessengerMessage;

/// <summary>
/// A <see cref="IFileManagerMessengerMessage"/> that carries a pane <see cref="Identity"/> so it can
/// be routed to a specific pane via <see cref="IdentityFilter.For{TMessage}"/>.
/// </summary>
public interface IIdentityMessage : IFileManagerMessengerMessage
{
    /// <summary>The pane/scope this message targets.</summary>
    public Identity Identity { get; }
}
