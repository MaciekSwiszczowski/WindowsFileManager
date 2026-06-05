namespace WinUiFileManager.Application.Messaging;

/// <summary>
/// A <see cref="IFileManagerMessengerMessage"/> that carries a pane <see cref="Identity"/> so it can
/// be routed to a specific pane by the messenger wrapper's identity-aware registration methods.
/// </summary>
public interface IIdentityMessage : IFileManagerMessengerMessage
{
    /// <summary>The pane/scope this message targets.</summary>
    public Identity Identity { get; }
}
