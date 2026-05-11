using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.Navigation;

/// <summary>
/// Navigation request to move a panel from its current folder into a child folder.
/// Consumed by <see cref="WinUiFileManager.Application.Navigation.PanelNavigationService"/>, which resolves and
/// validates the target path before publishing <see cref="WinUiFileManager.Application.Navigation.FileTableNavigateToPathMessage"/>.
/// </summary>
public sealed record FileTableNavigateDownRequestedMessage(string Identity, string FolderName) : IFileManagerMessengerMessage;
