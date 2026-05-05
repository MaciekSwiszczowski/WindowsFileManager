using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Resolved request to copy selected item paths to the clipboard.
/// </summary>
public sealed record CopyPathRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<FileSystemEntryModel> Items);
