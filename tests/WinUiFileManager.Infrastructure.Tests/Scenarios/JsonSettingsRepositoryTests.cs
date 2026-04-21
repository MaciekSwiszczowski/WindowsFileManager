using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Infrastructure.Persistence;

namespace WinUiFileManager.Infrastructure.Tests.Scenarios;

/// <summary>
/// Validates round-trip persistence and default loading behavior for the settings repository.
/// </summary>
/// <remarks>
/// These tests use the real app-data path at %LOCALAPPDATA%\WinUiFileManager\settings.json.
/// Each test backs up and restores the original file to avoid corrupting real settings.
/// </remarks>
public sealed class JsonSettingsRepositoryTests : IAsyncDisposable
{
    private readonly string _tempFilePath = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid()}.json");

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
                // Ignore cleanup
            }
        }
    }

    [Test]
    public async Task Test_LoadAsync_ReturnsDefaultsWhenNoFile()
    {
        // Arrange
        var sut = CreateRepository();

        // Act
        var settings = await sut.LoadAsync(CancellationToken.None);

        // Assert
        var defaults = new AppSettings();
        await Assert.That(settings.ParallelExecutionEnabled).IsEqualTo(defaults.ParallelExecutionEnabled);
        await Assert.That(settings.MaxDegreeOfParallelism).IsEqualTo(defaults.MaxDegreeOfParallelism);
        await Assert.That(settings.LastActivePane).IsEqualTo(defaults.LastActivePane);
    }

    [Test]
    public async Task Test_SaveAndLoad_RoundTrips()
    {
        // Arrange
        var sut = CreateRepository();
        var settings = new AppSettings(
            true,
            8,
            NormalizedPath.FromUserInput(@"C:\Left"),
            NormalizedPath.FromUserInput(@"C:\Right"),
            PaneId.Right);

        // Act
        await sut.SaveAsync(settings, CancellationToken.None);
        var loaded = await sut.LoadAsync(CancellationToken.None);

        // Assert
        await Assert.That(loaded.ParallelExecutionEnabled).IsTrue();
        await Assert.That(loaded.MaxDegreeOfParallelism).IsEqualTo(8);
        await Assert.That(loaded.LastActivePane).IsEqualTo(PaneId.Right);
    }

    [Test]
    public async Task Test_SaveAndLoad_PreservesAllSettings()
    {
        // Arrange
        var sut = CreateRepository();
        var settings = new AppSettings(
            true,
            16,
            NormalizedPath.FromUserInput(@"C:\Users"),
            NormalizedPath.FromUserInput(@"C:\Temp"),
            PaneId.Left);

        // Act
        await sut.SaveAsync(settings, CancellationToken.None);
        var reader = CreateRepository();
        var loaded = await reader.LoadAsync(CancellationToken.None);

        // Assert
        await Assert.That(loaded.ParallelExecutionEnabled).IsEqualTo(settings.ParallelExecutionEnabled);
        await Assert.That(loaded.MaxDegreeOfParallelism).IsEqualTo(settings.MaxDegreeOfParallelism);
        await Assert.That(loaded.LastLeftPanePath!.Value.DisplayPath).IsEqualTo(@"C:\Users");
        await Assert.That(loaded.LastRightPanePath!.Value.DisplayPath).IsEqualTo(@"C:\Temp");
        await Assert.That(loaded.LastActivePane).IsEqualTo(settings.LastActivePane);
    }

    [Test]
    public async Task Test_SaveAndLoad_RoundTripsLayoutFields()
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
                IsMaximized: false));

        // Act
        await sut.SaveAsync(settings, CancellationToken.None);
        var loaded = await CreateRepository().LoadAsync(CancellationToken.None);

        // Assert
        await Assert.That(loaded.LeftPaneWidth).IsEqualTo(512d);
        await Assert.That(loaded.InspectorWidth).IsEqualTo(400d);
        await Assert.That(loaded.LeftPaneColumns.NameWidth).IsEqualTo(350d);
        await Assert.That(loaded.LeftPaneColumns.ModifiedWidth).IsEqualTo(140d);
        await Assert.That(loaded.LeftPaneSort.Column).IsEqualTo(SortColumn.Size);
        await Assert.That(loaded.LeftPaneSort.Ascending).IsFalse();
        await Assert.That(loaded.RightPaneSort.Column).IsEqualTo(SortColumn.Modified);
        await Assert.That(loaded.MainWindowPlacement.X).IsEqualTo(120);
        await Assert.That(loaded.MainWindowPlacement.Width).IsEqualTo(1600);
        await Assert.That(loaded.MainWindowPlacement.IsMaximized).IsFalse();
    }

    [Test]
    public async Task Test_LoadAsync_UsesDefaultsForMissingLayoutFields()
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
        await Assert.That(loaded.LeftPaneWidth).IsEqualTo(600d);
        await Assert.That(loaded.LeftPaneColumns).IsEqualTo(PaneColumnLayout.Default);
        await Assert.That(loaded.LeftPaneSort).IsEqualTo(SortState.Default);
        await Assert.That(loaded.MainWindowPlacement).IsEqualTo(WindowPlacement.Default);
    }

    private JsonSettingsRepository CreateRepository()
    {
        return new JsonSettingsRepository(NullLogger<JsonSettingsRepository>.Instance, _tempFilePath);
    }
}
