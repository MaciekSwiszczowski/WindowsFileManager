using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Resolved request to open the Windows file-properties dialog for an item. Produced by the pane
/// behavior from a <see cref="WinUiFileManager.Application.Messages.PropertiesKeyPressedMessage"/>; handled via <see cref="WinUiFileManager.Application.Abstractions.IShellService"/>.
/// </summary>
/// <param name="SourceIdentity">Identity of the pane the request originated in.</param>
/// <param name="Item">The entry whose properties to show.</param>
public sealed record PropertiesRequestedMessage(
    string SourceIdentity,
    FileSystemEntryModel Item) : IFileManagerMessengerMessage;
