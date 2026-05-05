using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Resolved request to delete selected items.
/// </summary>
public sealed record DeleteRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<FileSystemEntryModel> Items);
