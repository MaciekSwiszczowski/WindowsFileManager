namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Published when navigation up is requested.
/// </summary>
public sealed record FileTableNavigateUpRequestedMessage(string Identity);
