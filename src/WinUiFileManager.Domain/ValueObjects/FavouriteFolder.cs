namespace WinUiFileManager.Domain.ValueObjects;

public sealed record FavouriteFolder(
    FavouriteFolderId Id,
    string DisplayName,
    NormalizedPath Path);
