namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ViewModelFavouritesCommandTests
{
    [Test]
    public async Task Test_AddFavourite_AddsCurrentDirectoryToFavourites()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source");
        var destDir = fixture.CreateDirectory("dest");

        var builder = new ViewModelTestBuilder();
        using var vm = builder.Build();
        vm.LeftPane.PaneId = PaneId.Left;
        vm.RightPane.PaneId = PaneId.Right;

        await vm.LeftPane.NavigateToCommand.ExecuteAsync(sourceDir);
        await vm.RightPane.NavigateToCommand.ExecuteAsync(destDir);

        // Act
        await vm.AddFavouriteCommand.ExecuteAsync(null);

        // Assert
        var favourites = await builder.FavouritesRepository.GetAllAsync(CancellationToken.None);
        await Assert.That(favourites.Count).IsEqualTo(1);
        await Assert.That(favourites[0].Path.DisplayPath).Contains("source");
        await Assert.That(vm.Favourites.Count).IsEqualTo(1);
    }
}
