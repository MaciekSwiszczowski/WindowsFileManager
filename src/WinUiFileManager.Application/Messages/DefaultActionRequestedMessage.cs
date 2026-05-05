using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Resolved request to open a file or enter a directory.
/// </summary>
public sealed record DefaultActionRequestedMessage(
    string SourceIdentity,
    FileSystemEntryModel Item);
