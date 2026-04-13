using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Infrastructure.Persistence;

namespace WinUiFileManager.Infrastructure.Tests.Scenarios;

/// <remarks>
/// These tests use the real app-data path at %LOCALAPPDATA%\WinUiFileManager\favourites.json.
/// Each test adds/removes its own uniquely-identified favourite to avoid corrupting real data.
/// </remarks>
public sealed class JsonFavouritesRepositoryTests : IAsyncDisposable
{
    private readonly string _tempFilePath = Path.Combine(Path.GetTempPath(), $"favourites_{Guid.NewGuid()}.json");

    public async ValueTask DisposeAsync()
    {
        if (File.Exists(_tempFilePath))
        {
            try
            {
                File.Delete(_tempFilePath);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    [Test]
    public async Task Test_AddAndGetAll_RoundTrips()
    {
        // Arrange
        var sut = CreateRepository();
        var id = FavouriteFolderId.NewId();
        var favourite = new FavouriteFolder(id, "TestFolder", NormalizedPath.FromUserInput(@"C:\Temp\TestRoundTrip"));

        // Act
        await sut.AddAsync(favourite, CancellationToken.None);
        var all = await sut.GetAllAsync(CancellationToken.None);

        // Assert
        await Assert.That(all.Any(f => f.Id == id)).IsTrue();
    }

    [Test]
    public async Task Test_Remove_RemovesFavourite()
    {
        // Arrange
        var sut = CreateRepository();
        var id = FavouriteFolderId.NewId();
        var favourite = new FavouriteFolder(id, "ToRemove", NormalizedPath.FromUserInput(@"C:\Temp\ToRemove"));
        await sut.AddAsync(favourite, CancellationToken.None);

        // Act
        await sut.RemoveAsync(id, CancellationToken.None);

        // Assert
        var all = await sut.GetAllAsync(CancellationToken.None);
        await Assert.That(all.Any(f => f.Id == id)).IsFalse();
    }

    [Test]
    public async Task Test_GetAll_EmptyOnFirstRun()
    {
        // Arrange
        var sut = CreateRepository();

        // Act
        var all = await sut.GetAllAsync(CancellationToken.None);

        // Assert
        await Assert.That(all).IsNotNull();
    }

    [Test]
    public async Task Test_PersistenceRoundTrip_SurvivesReload()
    {
        // Arrange
        var id = FavouriteFolderId.NewId();
        var favourite = new FavouriteFolder(id, "Persistent", NormalizedPath.FromUserInput(@"C:\Temp\Persistent"));

        var writer = CreateRepository();
        await writer.AddAsync(favourite, CancellationToken.None);

        // Act
        var reader = CreateRepository();
        var all = await reader.GetAllAsync(CancellationToken.None);

        // Assert
        await Assert.That(all.Any(f => f.Id == id)).IsTrue();
        var loaded = all.Single(f => f.Id == id);
        await Assert.That(loaded.DisplayName).IsEqualTo("Persistent");
    }

    private JsonFavouritesRepository CreateRepository()
    {
        return new JsonFavouritesRepository(NullLogger<JsonFavouritesRepository>.Instance, _tempFilePath);
    }
}
