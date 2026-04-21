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
            dto.ParallelExecutionEnabled,
            dto.MaxDegreeOfParallelism,
            string.IsNullOrEmpty(dto.LastLeftPanePath)
                ? (NormalizedPath?)null
                : NormalizedPath.FromUserInput(dto.LastLeftPanePath),
            string.IsNullOrEmpty(dto.LastRightPanePath)
                ? (NormalizedPath?)null
                : NormalizedPath.FromUserInput(dto.LastRightPanePath),
            Enum.TryParse<PaneId>(dto.LastActivePane, ignoreCase: true, out var pane)
                ? pane
                : PaneId.Left,
            dto.InspectorVisible,
            dto.InspectorWidth > 0 ? dto.InspectorWidth : 340d);

    private static SettingsDto ToDto(AppSettings settings) =>
        new(
            settings.ParallelExecutionEnabled,
            settings.MaxDegreeOfParallelism,
            settings.LastLeftPanePath?.DisplayPath,
            settings.LastRightPanePath?.DisplayPath,
            settings.LastActivePane.ToString(),
            settings.InspectorVisible,
            settings.InspectorWidth);
}
