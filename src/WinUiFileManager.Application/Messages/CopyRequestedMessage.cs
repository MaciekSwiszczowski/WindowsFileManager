using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Resolved request to copy selected items.
/// </summary>
public sealed record CopyRequestedMessage(
    string SourceIdentity,
    string TargetIdentity,
    IReadOnlyList<FileSystemEntryModel> Items);
