using System.Text.Json;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Infrastructure.Persistence;

internal sealed class JsonSettingsRepository : ISettingsRepository
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<JsonSettingsRepository> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public JsonSettingsRepository(ILogger<JsonSettingsRepository> logger)
        : this(logger, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinUiFileManager",
            "settings.json"))
    {
    }

    public JsonSettingsRepository(ILogger<JsonSettingsRepository> logger, string filePath)
    {
        _logger = logger;
        _filePath = filePath;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            try
            {
                await using var stream = File.OpenRead(_filePath);
                var dto = await JsonSerializer.DeserializeAsync<SettingsDto>(
                    stream, JsonOptions, cancellationToken);

                return dto is not null ? ToDomain(dto) : new AppSettings();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize settings from {Path}, returning defaults", _filePath);
                return new AppSettings();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);

            var dto = ToDto(settings);
            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static AppSettings ToDomain(SettingsDto dto) =>
        new(
            parallelExecutionEnabled: dto.ParallelExecutionEnabled,
            maxDegreeOfParallelism: dto.MaxDegreeOfParallelism,
            lastLeftPanePath: string.IsNullOrEmpty(dto.LastLeftPanePath)
                ? (NormalizedPath?)null
                : NormalizedPath.FromUserInput(dto.LastLeftPanePath),
            lastRightPanePath: string.IsNullOrEmpty(dto.LastRightPanePath)
                ? (NormalizedPath?)null
                : NormalizedPath.FromUserInput(dto.LastRightPanePath),
            lastActivePane: Enum.TryParse<PaneId>(dto.LastActivePane, ignoreCase: true, out var pane)
                ? pane
                : PaneId.Left,
            inspectorVisible: dto.InspectorVisible,
            inspectorWidth: dto.InspectorWidth > 0 ? dto.InspectorWidth : 340d,
            leftPaneWidth: dto.LeftPaneWidth is > 0 ? dto.LeftPaneWidth.Value : 600d,
            leftPaneColumns: ToDomain(dto.LeftPaneColumns),
            rightPaneColumns: ToDomain(dto.RightPaneColumns),
            leftPaneSort: ToDomain(dto.LeftPaneSort),
            rightPaneSort: ToDomain(dto.RightPaneSort),
            mainWindowPlacement: ToDomain(dto.MainWindowPlacement));

    private static PaneColumnLayout? ToDomain(PaneColumnLayoutDto? dto) =>
        dto is null
            ? null
            : new PaneColumnLayout(
                NameWidth: dto.NameWidth > 0 ? dto.NameWidth : PaneColumnLayout.Default.NameWidth,
                ExtensionWidth: dto.ExtensionWidth > 0 ? dto.ExtensionWidth : PaneColumnLayout.Default.ExtensionWidth,
                SizeWidth: dto.SizeWidth > 0 ? dto.SizeWidth : PaneColumnLayout.Default.SizeWidth,
                ModifiedWidth: dto.ModifiedWidth > 0 ? dto.ModifiedWidth : PaneColumnLayout.Default.ModifiedWidth,
                AttributesWidth: dto.AttributesWidth > 0 ? dto.AttributesWidth : PaneColumnLayout.Default.AttributesWidth);

    private static SortState? ToDomain(SortStateDto? dto) =>
        dto is null
            ? null
            : new SortState(
                Column: Enum.TryParse<SortColumn>(dto.Column, ignoreCase: true, out var column)
                    ? column
                    : SortColumn.Name,
                Ascending: dto.Ascending);

    private static WindowPlacement? ToDomain(WindowPlacementDto? dto) =>
        dto is null
            ? null
            : new WindowPlacement(
                X: dto.X,
                Y: dto.Y,
                Width: dto.Width > 0 ? dto.Width : WindowPlacement.Default.Width,
                Height: dto.Height > 0 ? dto.Height : WindowPlacement.Default.Height,
                IsMaximized: dto.IsMaximized);

    private static SettingsDto ToDto(AppSettings settings) =>
        new()
        {
            ParallelExecutionEnabled = settings.ParallelExecutionEnabled,
            MaxDegreeOfParallelism = settings.MaxDegreeOfParallelism,
            LastLeftPanePath = settings.LastLeftPanePath?.DisplayPath,
            LastRightPanePath = settings.LastRightPanePath?.DisplayPath,
            LastActivePane = settings.LastActivePane.ToString(),
            InspectorVisible = settings.InspectorVisible,
            InspectorWidth = settings.InspectorWidth,
            LeftPaneWidth = settings.LeftPaneWidth,
            LeftPaneColumns = ToDto(settings.LeftPaneColumns),
            RightPaneColumns = ToDto(settings.RightPaneColumns),
            LeftPaneSort = ToDto(settings.LeftPaneSort),
            RightPaneSort = ToDto(settings.RightPaneSort),
            MainWindowPlacement = ToDto(settings.MainWindowPlacement)
        };

    private static PaneColumnLayoutDto ToDto(PaneColumnLayout layout) =>
        new()
        {
            NameWidth = layout.NameWidth,
            ExtensionWidth = layout.ExtensionWidth,
            SizeWidth = layout.SizeWidth,
            ModifiedWidth = layout.ModifiedWidth,
            AttributesWidth = layout.AttributesWidth
        };

    private static SortStateDto ToDto(SortState state) =>
        new()
        {
            Column = state.Column.ToString(),
            Ascending = state.Ascending
        };

    private static WindowPlacementDto ToDto(WindowPlacement placement) =>
        new()
        {
            X = placement.X,
            Y = placement.Y,
            Width = placement.Width,
            Height = placement.Height,
            IsMaximized = placement.IsMaximized
        };
}
