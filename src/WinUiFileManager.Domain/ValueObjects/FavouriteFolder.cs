namespace WinUiFileManager.Domain.ValueObjects;

public sealed record FavouriteFolder
{
    public FavouriteFolder(FavouriteFolderId id, string displayName, NormalizedPath path)
    {
        Id = id;
        DisplayName = displayName;
        Path = path;
    }

    public FavouriteFolderId Id { get; init; }

    public string DisplayName { get; init; }

    public NormalizedPath Path { get; init; }
}
