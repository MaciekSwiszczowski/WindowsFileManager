namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Resolved domain message published by the coordinator.
/// Consumed by the domain services to prompt for a new folder name.
/// </summary>
public sealed record CreateFolderRequestedMessage(string SourceIdentity);
