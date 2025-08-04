using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace MMM.Fabric;

[JsonSerializable(typeof(FabricMod))]
public partial class FabricModJsonSerializerContext : JsonSerializerContext
{

}

public class GenericComponent
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("version")] public required string Version { get; init; }

    // Metadata

    [JsonPropertyName("name")] public string? Name { get; init; }

    public override string ToString() => $"{Name ?? Id} ({Version})";
}

public class FabricMod : GenericComponent
{
    // Mod loading

    [JsonConverter(typeof(ItemsJsonConverter<string>))]
    [JsonPropertyName("environment")] public Items<string>? Environment { get; init; }
    [JsonPropertyName("jars")] public ImmutableArray<NestedJarEntry>? Jars { get; init; }
    [JsonPropertyName("provides")] public ImmutableArray<string> Provides { get; init; }

    // Dependency resolution

    [JsonPropertyName("depends")] public Dictionary<string, VersionRange>? Depends { get; init; }
    [JsonPropertyName("recommends")] public Dictionary<string, VersionRange>? Recommends { get; init; }
    [JsonPropertyName("suggests")] public Dictionary<string, VersionRange>? Suggests { get; init; }
    [JsonPropertyName("conflicts")] public Dictionary<string, VersionRange>? Conflicts { get; init; }
    [JsonPropertyName("breaks")] public Dictionary<string, VersionRange>? Breaks { get; init; }
}
