namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeFavouritesRepository : IFavouritesRepository
{
    private readonly List<FavouriteFolder> _items = [];

    public Task<IReadOnlyList<FavouriteFolder>> GetAllAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<FavouriteFolder>>(_items.ToList());

    public Task AddAsync(FavouriteFolder favourite, CancellationToken cancellationToken)
    {
        _items.Add(favourite);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(FavouriteFolderId id, CancellationToken cancellationToken)
    {
        _items.RemoveAll(f => f.Id == id);
        return Task.CompletedTask;
    }
}
