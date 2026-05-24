using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Navigation;

/// <summary>
/// Navigation command to move a panel to an accepted normalized path.
/// This is the only navigation message panels and table data sources should consume.
/// </summary>
public sealed record FileTableNavigateToPathMessage(Identity Identity, NormalizedPath Path) : IIdentityMessage;
