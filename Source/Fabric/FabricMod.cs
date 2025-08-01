using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MMM.Fabric;

public static class Utf8JsonReaderExtensions
{
    public static void SkipJunk(this ref Utf8JsonReader reader)
    {
        while (reader.TokenType is JsonTokenType.Comment or JsonTokenType.None)
        {
            reader.Skip();
        }
    }
}

[JsonSerializable(typeof(FabricMod))]
public partial class FabricModJsonSerializerContext : JsonSerializerContext
{

}

public class FabricMod
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("version")] public required string Version { get; init; }

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

    // Metadata

    [JsonPropertyName("name")] public string? Name { get; init; }

    public override string ToString() => $"{Name ?? Id} ({Version})";
}
