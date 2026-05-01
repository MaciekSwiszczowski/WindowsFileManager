namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Outgoing control message published when entering a folder is requested.
/// </summary>
public sealed record FileTableNavigateDownRequestedMessage(
    string Identity,
    SpecFileEntryViewModel Item);
