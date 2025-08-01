using System.Text.Json.Serialization;

namespace MMM;

public class ModlistEntry
{
    [JsonPropertyName("id")] public required string Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }

    public override string ToString() => $"{Name ?? Id}";
}

public class Modlist
{
    [JsonPropertyName("loader")] public required string Loader { get; set; }
    [JsonPropertyName("gameVersion")] public required string GameVersion { get; set; }
    [JsonPropertyName("defaultAllowedReleaseTypes")] public required List<string> DefaultAllowedReleaseTypes { get; set; }
    [JsonPropertyName("modsFolder")] public required string ModsFolder { get; set; }
    [JsonPropertyName("mods")] public required List<ModlistEntry> Mods { get; set; }
}