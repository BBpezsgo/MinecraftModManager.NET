using System.Text.Json.Serialization;

namespace MMM;

public class ModList
{
    [JsonPropertyName("loader")] public required string Loader { get; init; }
    [JsonPropertyName("gameVersion")] public required string GameVersion { get; set; }
    [JsonPropertyName("modsFolder")] public required string ModsFolder { get; init; }
    [JsonPropertyName("mods")] public required List<ModEntry> Mods { get; init; }
}
