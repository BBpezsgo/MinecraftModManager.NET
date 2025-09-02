using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace MMM.Fabric;

public class FabricMod : IMod
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("version")] public required string Version { get; init; }

    // Mod loading

    [JsonConverter(typeof(ItemsJsonConverter<string>))]
    [JsonPropertyName("environment")] public Items<string>? Environment { get; init; }
    [JsonPropertyName("jars")] public ImmutableArray<FabricNestedJarEntry>? Jars { get; init; }
    [JsonPropertyName("provides")] public ImmutableArray<string> Provides { get; init; }

    // Dependency resolution

    [JsonPropertyName("depends")] public Dictionary<string, FabricVersionRange>? Depends { get; init; }
    [JsonPropertyName("recommends")] public Dictionary<string, FabricVersionRange>? Recommends { get; init; }
    [JsonPropertyName("suggests")] public Dictionary<string, FabricVersionRange>? Suggests { get; init; }
    [JsonPropertyName("conflicts")] public Dictionary<string, FabricVersionRange>? Conflicts { get; init; }
    [JsonPropertyName("breaks")] public Dictionary<string, FabricVersionRange>? Breaks { get; init; }

    // Metadata

    [JsonPropertyName("name")] public string? Name { get; init; }

    public override string ToString() => $"{Name ?? Id} ({Version})";
}
