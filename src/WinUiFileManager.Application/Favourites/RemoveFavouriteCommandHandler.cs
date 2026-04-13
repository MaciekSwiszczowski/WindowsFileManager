using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Favourites;

public sealed class RemoveFavouriteCommandHandler
{
    private readonly IFavouritesRepository _favouritesRepository;
    private readonly ILogger<RemoveFavouriteCommandHandler> _logger;

    public RemoveFavouriteCommandHandler(
        IFavouritesRepository favouritesRepository,
        ILogger<RemoveFavouriteCommandHandler> logger)
    {
        _favouritesRepository = favouritesRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync(FavouriteFolderId id, CancellationToken ct)
    {
        await _favouritesRepository.RemoveAsync(id, ct);
        _logger.LogInformation("Removed favourite: {Id}", id);
    }
}
