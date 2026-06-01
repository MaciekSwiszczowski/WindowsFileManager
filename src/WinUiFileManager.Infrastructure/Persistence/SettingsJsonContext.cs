using System.Text.Json.Serialization;

namespace WinUiFileManager.Infrastructure.Persistence;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(SettingsDto))]
[JsonSerializable(typeof(PaneColumnLayoutDto))]
[JsonSerializable(typeof(SortStateDto))]
[JsonSerializable(typeof(WindowPlacementDto))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
