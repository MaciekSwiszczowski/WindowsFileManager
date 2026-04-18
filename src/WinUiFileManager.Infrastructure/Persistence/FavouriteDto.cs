namespace WinUiFileManager.Infrastructure.Persistence;

internal sealed record FavouriteDto
{
    public FavouriteDto(string id, string displayName, string path)
    {
        Id = id;
        DisplayName = displayName;
        Path = path;
    }

    public string Id { get; init; }

    public string DisplayName { get; init; }

    public string Path { get; init; }
}
