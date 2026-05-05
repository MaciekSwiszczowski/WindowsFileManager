using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Messages;

public sealed record PropertiesRequestedMessage(
    string SourceIdentity,
    FileSystemEntryModel Item);
