using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Application.Abstractions;

public interface IFavouritesRepository
{
    Task<IReadOnlyList<FavouriteFolder>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(FavouriteFolder favourite, CancellationToken cancellationToken);
    Task RemoveAsync(FavouriteFolderId id, CancellationToken cancellationToken);
}
