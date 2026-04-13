using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Errors;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Favourites;

public sealed class AddFavouriteCommandHandler
{
    private readonly IFavouritesRepository _favouritesRepository;
    private readonly INtfsVolumePolicyService _ntfsVolumePolicyService;
    private readonly ILogger<AddFavouriteCommandHandler> _logger;

    public AddFavouriteCommandHandler(
        IFavouritesRepository favouritesRepository,
        INtfsVolumePolicyService ntfsVolumePolicyService,
        ILogger<AddFavouriteCommandHandler> logger)
    {
        _favouritesRepository = favouritesRepository;
        _ntfsVolumePolicyService = ntfsVolumePolicyService;
        _logger = logger;
    }

    public async Task<PathValidationResult> ExecuteAsync(
        string displayName,
        NormalizedPath path,
        CancellationToken ct)
    {
        var validation = _ntfsVolumePolicyService.ValidateNtfsPath(path.Value);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Cannot add favourite, invalid NTFS path: {Path}", path);
            return validation;
        }

        var favourite = new FavouriteFolder(FavouriteFolderId.NewId(), displayName, path);
        await _favouritesRepository.AddAsync(favourite, ct);

        _logger.LogInformation("Added favourite: {Name} -> {Path}", displayName, path);
        return PathValidationResult.Valid();
    }
}
