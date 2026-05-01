namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Resolved domain message published by the coordinator.
/// Consumed by the file operation dialog service to initiate the copy destination dialog.
/// </summary>
public sealed record CopyRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<SpecFileEntryViewModel> Items);
