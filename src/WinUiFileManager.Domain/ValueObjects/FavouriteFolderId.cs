namespace WinUiFileManager.Domain.ValueObjects;

public readonly record struct FavouriteFolderId(Guid Value)
{
    public static FavouriteFolderId NewId() => new(Guid.NewGuid());
}
