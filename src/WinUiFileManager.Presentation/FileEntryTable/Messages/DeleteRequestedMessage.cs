namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Resolved domain message published by the coordinator.
/// Consumed by the domain services to perform deletion after confirmation.
/// </summary>
public sealed record DeleteRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<SpecFileEntryViewModel> Items);
