namespace WinUiFileManager.Domain.ValueObjects;

public readonly record struct FavouriteFolderId
{
    public FavouriteFolderId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; init; }

    public static FavouriteFolderId NewId() => new(Guid.NewGuid());
}
