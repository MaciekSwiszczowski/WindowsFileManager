using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Messages;

public sealed record RenameRequestedMessage(
    string SourceIdentity,
    FileSystemEntryModel Item);
