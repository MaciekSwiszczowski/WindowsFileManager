using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Resolved request to navigate a specific pane to its parent directory. Produced by the pane behavior
/// from a <see cref="NavigateUpKeyPressedMessage"/>.
/// </summary>
/// <remarks>
/// Distinct from <see cref="Navigation.FileTableNavigateUpRequestedMessage"/>: that one is
/// identity-typed and consumed by <see cref="WinUiFileManager.Application.Navigation.PanelNavigationService"/>;
/// this one carries the source identity as a raw string for the broader command pipeline.
/// </remarks>
/// <param name="SourceIdentity">Identity of the pane to navigate up.</param>
public sealed record NavigateUpRequestedMessage(string SourceIdentity) : IFileManagerMessengerMessage;
