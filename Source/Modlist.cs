using System.Collections.Immutable;
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
    [JsonPropertyName("loader")] public required string Loader { get; init; }
    [JsonPropertyName("gameVersion")] public required string GameVersion { get; init; }
    [JsonPropertyName("defaultAllowedReleaseTypes")] public required ImmutableArray<string> DefaultAllowedReleaseTypes { get; init; }
    [JsonPropertyName("modsFolder")] public required string ModsFolder { get; init; }
    [JsonPropertyName("mods")] public required List<ModlistEntry> Mods { get; init; }
}