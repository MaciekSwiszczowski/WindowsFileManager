using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Resolved request to move selected items.
/// </summary>
public sealed record MoveRequestedMessage(
    string SourceIdentity,
    string TargetIdentity,
    IReadOnlyList<FileSystemEntryModel> Items);
