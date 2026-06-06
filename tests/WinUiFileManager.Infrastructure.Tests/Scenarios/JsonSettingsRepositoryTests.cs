using Microsoft.Extensions.Logging.Abstractions;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.Infrastructure.Persistence;

namespace WinUiFileManager.Infrastructure.Tests.Scenarios;

/// <summary>
/// Validates round-trip persistence and default loading behavior for the settings repository.
/// </summary>
/// <remarks>
/// These tests use the real app-data path at %LOCALAPPDATA%\WinUiFileManager\settings.json.
/// Each test backs up and restores the original file to avoid corrupting real settings.
/// </remarks>
public sealed class JsonSettingsRepositoryTests : IAsyncLifetime
{
    private readonly string _tempFilePath = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid()}.json");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (File.Exists(_tempFilePath))
        {
            try
            {
                File.Delete(_tempFilePath);
            }
            catch
            {
                // Ignore cleanup
            }
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWhenNoFile()
    {
        // Arrange
        var sut = CreateRepository();

        // Act
        var settings = await sut.LoadAsync(CancellationToken.None);

        // Assert
        var defaults = new AppSettings();
        Assert.Equal(defaults.ParallelExecutionEnabled, settings.ParallelExecutionEnabled);
        Assert.Equal(defaults.MaxDegreeOfParallelism, settings.MaxDegreeOfParallelism);
        Assert.Equal(defaults.LastActivePane, settings.LastActivePane);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        // Arrange
        var sut = CreateRepository();
        var settings = new AppSettings(
            true,
            8,
            NormalizedPath.FromUserInput(@"C:\Left"),
            NormalizedPath.FromUserInput(@"C:\Right"),
            "Right");

        // Act
        await sut.SaveAsync(settings, CancellationToken.None);
        var loaded = await sut.LoadAsync(CancellationToken.None);

        // Assert
        Assert.True(loaded.ParallelExecutionEnabled);
        Assert.Equal(8, loaded.MaxDegreeOfParallelism);
        Assert.Equal("Right", loaded.LastActivePane);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesAllSettings()
    {
        // Arrange
        var sut = CreateRepository();
        var settings = new AppSettings(
            true,
            16,
            NormalizedPath.FromUserInput(@"C:\Users"),
            NormalizedPath.FromUserInput(@"C:\Temp"),
            "Left");

        // Act
        await sut.SaveAsync(settings, CancellationToken.None);
        var reader = CreateRepository();
        var loaded = await reader.LoadAsync(CancellationToken.None);

        // Assert
        Assert.Equal(settings.ParallelExecutionEnabled, loaded.ParallelExecutionEnabled);
        Assert.Equal(settings.MaxDegreeOfParallelism, loaded.MaxDegreeOfParallelism);
        Assert.Equal(@"C:\Users", loaded.LastLeftPanePath!.Value.DisplayPath);
        Assert.Equal(@"C:\Temp", loaded.LastRightPanePath!.Value.DisplayPath);
        Assert.Equal(settings.LastActivePane, loaded.LastActivePane);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsLayoutFields()
    {
        // Arrange
        var sut = CreateRepository();
        var settings = new AppSettings(
            leftPaneWidth: 512d,
            inspectorWidth: 400d,
            leftPaneColumns: new PaneColumnLayout(
                NameWidth: 350d,
                ExtensionWidth: 45d,
                SizeWidth: 80d,
                ModifiedWidth: 140d,
                AttributesWidth: 55d),
            rightPaneColumns: PaneColumnLayout.Default,
            leftPaneSort: new SortState(SortColumn.Size, Ascending: false),
            rightPaneSort: new SortState(SortColumn.Modified, Ascending: true),
            mainWindowPlacement: new WindowPlacement(
                X: 120,
                Y: 80,
                Width: 1600,
                Height: 1000,
                IsMaximized: false,
                DisplayDeviceName: @"\\.\DISPLAY1"));

        // Act
        await sut.SaveAsync(settings, CancellationToken.None);
        var loaded = await CreateRepository().LoadAsync(CancellationToken.None);

        // Assert
        Assert.Equal(512d, loaded.LeftPaneWidth);
        Assert.Equal(400d, loaded.InspectorWidth);
        Assert.Equal(350d, loaded.LeftPaneColumns.NameWidth);
        Assert.Equal(140d, loaded.LeftPaneColumns.ModifiedWidth);
        Assert.Equal(SortColumn.Size, loaded.LeftPaneSort.Column);
        Assert.False(loaded.LeftPaneSort.Ascending);
        Assert.Equal(SortColumn.Modified, loaded.RightPaneSort.Column);
        Assert.Equal(120, loaded.MainWindowPlacement.X);
        Assert.Equal(1600, loaded.MainWindowPlacement.Width);
        Assert.False(loaded.MainWindowPlacement.IsMaximized);
        Assert.Equal(@"\\.\DISPLAY1", loaded.MainWindowPlacement.DisplayDeviceName);
    }

    [Fact]
    public async Task LoadAsync_UsesDefaultsForMissingLayoutFields()
    {
        // Arrange
        var legacyJson = """
            {
              "parallelExecutionEnabled": false,
              "maxDegreeOfParallelism": 4,
              "lastLeftPanePath": null,
              "lastRightPanePath": null,
              "lastActivePane": "Left",
              "inspectorVisible": true,
              "inspectorWidth": 340
            }
            """;
        await File.WriteAllTextAsync(_tempFilePath, legacyJson, CancellationToken.None);
        var sut = CreateRepository();

        // Act
        var loaded = await sut.LoadAsync(CancellationToken.None);

        // Assert
        Assert.Equal(600d, loaded.LeftPaneWidth);
        Assert.Equal(PaneColumnLayout.Default, loaded.LeftPaneColumns);
        Assert.Equal(SortState.Default, loaded.LeftPaneSort);
        Assert.Equal(WindowPlacement.Default, loaded.MainWindowPlacement);
    }

    private JsonSettingsRepository CreateRepository()
    {
        return new JsonSettingsRepository(NullLogger<JsonSettingsRepository>.Instance, _tempFilePath);
    }
}
