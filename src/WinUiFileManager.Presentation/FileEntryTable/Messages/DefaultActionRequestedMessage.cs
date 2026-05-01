namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Resolved domain message published by the coordinator.
/// Consumed by the shell/navigation services to open a file or enter a directory.
/// </summary>
public sealed record DefaultActionRequestedMessage(
    string SourceIdentity,
    SpecFileEntryViewModel Item);
