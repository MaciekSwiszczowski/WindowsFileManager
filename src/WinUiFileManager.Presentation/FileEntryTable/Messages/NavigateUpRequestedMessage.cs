namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Resolved domain message published by the coordinator.
/// Consumed by the navigation service to change the current directory.
/// </summary>
public sealed record NavigateUpRequestedMessage(string SourceIdentity);
