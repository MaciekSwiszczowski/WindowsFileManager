using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.Navigation;

/// <summary>
/// Navigation request to move a panel to a specific normalized path.
/// Consumed by <see cref="WinUiFileManager.Application.Navigation.PanelNavigationService"/>, which validates and
/// stores the target path before publishing <see cref="WinUiFileManager.Application.Navigation.FileTableNavigateToPathMessage"/>.
/// </summary>
public sealed record FileTableNavigateToPathRequestedMessage(Identity Identity, NormalizedPath Path) : IIdentityMessage;
