using System.Text.Json;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Infrastructure.Persistence;

/// <summary>
/// Persists <see cref="AppSettings"/> to a JSON file under <c>%LocalAppData%\WinUiFileManager\settings.json</c>.
/// Infrastructure implementation of <see cref="ISettingsRepository"/>. Maps between the domain
/// <see cref="AppSettings"/> and the serialization-only <see cref="SettingsDto"/> graph so the on-disk schema can
/// evolve independently of the domain model and tolerate missing/garbage values.
/// </summary>
/// <remarks>
/// RESILIENCE: a missing file or a <see cref="JsonException"/> on load yields default settings rather than
/// throwing, so a corrupt settings file never blocks startup. Defaulting of individual fields (e.g. width
/// &gt; 0 fallbacks) happens in the
/// <c>ToDomain</c> mappers, keeping invalid persisted values from reaching the UI.
/// </remarks>
internal sealed class JsonSettingsRepository : ISettingsRepository
{
    private readonly string _filePath;
    private readonly ILogger<JsonSettingsRepository> _logger;

    /// <summary>Production constructor: targets <c>%LocalAppData%\WinUiFileManager\settings.json</c>.</summary>
    public JsonSettingsRepository(ILogger<JsonSettingsRepository> logger)
        : this(logger, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinUiFileManager",
            "settings.json"))
    {
    }

    /// <summary>Constructor that allows overriding the file path (used by tests).</summary>
    /// <param name="filePath">Absolute path to the settings file.</param>
    public JsonSettingsRepository(ILogger<JsonSettingsRepository> logger, string filePath)
    {
        _logger = logger;
        _filePath = filePath;
    }

    /// <summary>Loads settings from disk synchronously, returning defaults when the file is absent or unreadable.</summary>
    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            using var stream = File.OpenRead(_filePath);
            var dto = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.SettingsDto);

            // A null DTO means the file contained the JSON literal `null`; treat as "no settings".
            return dto is not null ? ToDomain(dto) : new AppSettings();
        }
        catch (JsonException ex)
        {
            // Corrupt/incompatible JSON must not break startup — log and fall back to defaults.
            _logger.LogWarning(ex, "Failed to deserialize settings from {Path}, returning defaults", _filePath);
            return new AppSettings();
        }
    }

    /// <summary>Loads settings from disk, returning defaults when the file is absent or unreadable.</summary>
    /// <param name="cancellationToken">Checked before the synchronous read starts.</param>
    /// <returns>The persisted settings, or a default <see cref="AppSettings"/> on missing/corrupt data.</returns>
    /// <exception cref="OperationCanceledException">If cancelled before reading.</exception>
    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Load());
    }

    /// <summary>Serializes <paramref name="settings"/> to disk, creating the target directory if needed.</summary>
    /// <param name="settings">The settings to persist.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="OperationCanceledException">If cancelled while writing.</exception>
    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        // Directory may not exist on first run; the production path is guaranteed to have a parent directory.
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var dto = ToDto(settings);
        // File.Create truncates any existing file before the serializer writes the new content.
        var stream = File.Create(_filePath);
        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(stream, dto, SettingsJsonContext.Default.SettingsDto, cancellationToken).ConfigureAwait(false);
        }
    }

    // DTO -> domain. This is where persisted values are sanitized: empty path strings become null NormalizedPath?,
    // non-positive widths fall back to sensible defaults, and an unknown active-pane defaults to "Left". This keeps
    // a hand-edited or downgraded settings file from producing an invalid in-memory model.
    private static AppSettings ToDomain(SettingsDto dto) =>
        new(
            parallelExecutionEnabled: dto.ParallelExecutionEnabled,
            maxDegreeOfParallelism: dto.MaxDegreeOfParallelism,
            lastLeftPanePath: string.IsNullOrEmpty(dto.LastLeftPanePath)
                ? null
                : NormalizedPath.FromUserInput(dto.LastLeftPanePath),
            lastRightPanePath: string.IsNullOrEmpty(dto.LastRightPanePath)
                ? null
                : NormalizedPath.FromUserInput(dto.LastRightPanePath),
            lastActivePane: string.IsNullOrWhiteSpace(dto.LastActivePane) ? "Left" : dto.LastActivePane,
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
            // Persisted column is a free-form string; an unrecognized/renamed value falls back to Name rather than throwing.
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
                IsMaximized: dto.IsMaximized,
                DisplayDeviceName: dto.DisplayDeviceName);

    // Domain -> DTO. NormalizedPath is stored as its DisplayPath (the \\?\ prefix is stripped) so the on-disk form
    // stays human-readable; FromUserInput re-applies the prefix on load.
    private static SettingsDto ToDto(AppSettings settings) =>
        new()
        {
            ParallelExecutionEnabled = settings.ParallelExecutionEnabled,
            MaxDegreeOfParallelism = settings.MaxDegreeOfParallelism,
            LastLeftPanePath = settings.LastLeftPanePath?.DisplayPath,
            LastRightPanePath = settings.LastRightPanePath?.DisplayPath,
            LastActivePane = settings.LastActivePane,
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
            IsMaximized = placement.IsMaximized,
            DisplayDeviceName = placement.DisplayDeviceName
        };
}
