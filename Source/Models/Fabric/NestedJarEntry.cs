using System.Text.Json.Serialization;

namespace MMM.Fabric;

public class FabricNestedJarEntry
{
    [JsonPropertyName("file")] public required string File { get; init; }
}
