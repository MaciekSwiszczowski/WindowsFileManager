namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Resolved domain message published by the coordinator.
/// Consumed by the file operation dialog service to initiate the move destination dialog.
/// </summary>
public sealed record MoveRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<SpecFileEntryViewModel> Items);
