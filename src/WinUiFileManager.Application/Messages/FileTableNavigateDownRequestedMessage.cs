using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Published when entering a folder is requested.
/// </summary>
public sealed record FileTableNavigateDownRequestedMessage(
    string Identity,
    FileSystemEntryModel Item);
