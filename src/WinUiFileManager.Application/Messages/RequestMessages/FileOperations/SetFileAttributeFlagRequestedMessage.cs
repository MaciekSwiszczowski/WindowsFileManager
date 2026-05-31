using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.FileOperations;

/// <summary>
/// Resolved request to set or clear a single NTFS attribute flag on a file. Sent by the inspector's
/// attribute-toggle view model (<c>InspectorAttributeToggleViewModel</c>) and handled by the Diagnostics
/// <c>FileOperationRequestHandler</c>.
/// </summary>
/// <param name="Path">The target file.</param>
/// <param name="Flag">The single attribute flag to change (e.g. <see cref="FileAttributes.Hidden"/>).</param>
/// <param name="Enabled"><see langword="true"/> to set the flag; <see langword="false"/> to clear it.</param>
public sealed record SetFileAttributeFlagRequestedMessage(NormalizedPath Path, FileAttributes Flag, bool Enabled) : IFileManagerMessengerMessage;
