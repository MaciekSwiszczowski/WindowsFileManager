using System.Text.Json;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Infrastructure.Persistence;

internal sealed class JsonFavouritesRepository : IFavouritesRepository
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<JsonFavouritesRepository> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public JsonFavouritesRepository(ILogger<JsonFavouritesRepository> logger)
        : this(logger, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinUiFileManager",
            "favourites.json"))
    {
    }

    public JsonFavouritesRepository(ILogger<JsonFavouritesRepository> logger, string filePath)
    {
        _logger = logger;
        _filePath = filePath;
    }

    public async Task<IReadOnlyList<FavouriteFolder>> GetAllAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var dtos = await LoadDtosAsync(cancellationToken);
            return dtos.Select(ToDomain).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task AddAsync(FavouriteFolder favourite, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var dtos = await LoadDtosAsync(cancellationToken);
            dtos.Add(ToDto(favourite));
            await SaveDtosAsync(dtos, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveAsync(FavouriteFolderId id, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var dtos = await LoadDtosAsync(cancellationToken);
            var idString = id.Value.ToString();
            dtos.RemoveAll(d => string.Equals(d.Id, idString, StringComparison.Ordinal));
            await SaveDtosAsync(dtos, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<FavouriteDto>> LoadDtosAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<List<FavouriteDto>>(stream, JsonOptions, cancellationToken)
                   ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize favourites from {Path}, returning empty list", _filePath);
            return [];
        }
    }

    private async Task SaveDtosAsync(List<FavouriteDto> dtos, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, dtos, JsonOptions, cancellationToken);
    }

    private static FavouriteFolder ToDomain(FavouriteDto dto) =>
        new(
            new FavouriteFolderId(Guid.Parse(dto.Id)),
            dto.DisplayName,
            NormalizedPath.FromUserInput(dto.Path));

    private static FavouriteDto ToDto(FavouriteFolder folder) =>
        new(
            folder.Id.Value.ToString(),
            folder.DisplayName,
            folder.Path.DisplayPath);
}
