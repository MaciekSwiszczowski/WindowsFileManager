using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Favourites;

public sealed class OpenFavouriteCommandHandler
{
    private readonly IFavouritesRepository _favouritesRepository;
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogger<OpenFavouriteCommandHandler> _logger;

    public OpenFavouriteCommandHandler(
        IFavouritesRepository favouritesRepository,
        IFileSystemService fileSystemService,
        ILogger<OpenFavouriteCommandHandler> logger)
    {
        _favouritesRepository = favouritesRepository;
        _fileSystemService = fileSystemService;
        _logger = logger;
    }

    public async Task<GoToPathResult> ExecuteAsync(FavouriteFolderId id, CancellationToken ct)
    {
        var favourites = await _favouritesRepository.GetAllAsync(ct);
        var favourite = favourites.FirstOrDefault(f => f.Id == id);

        if (favourite is null)
        {
            _logger.LogWarning("Favourite not found: {Id}", id);
            return new GoToPathResult(false, null, null, $"Favourite with id '{id.Value}' not found.");
        }

        var exists = await _fileSystemService.DirectoryExistsAsync(favourite.Path, ct);
        if (!exists)
        {
            _logger.LogWarning("Favourite directory no longer exists: {Path}", favourite.Path);
            return new GoToPathResult(false, favourite.Path, null, $"Directory not found: {favourite.Path.DisplayPath}");
        }

        _logger.LogDebug("Opening favourite: {Name} -> {Path}", favourite.DisplayName, favourite.Path);
        var entries = await _fileSystemService.EnumerateDirectoryAsync(favourite.Path, ct);
        return new GoToPathResult(true, favourite.Path, entries, null);
    }
}
