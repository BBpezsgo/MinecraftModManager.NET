using System.Text.Json.Serialization;

namespace MMM.Fabric;

public class NestedJarEntry
{
    [JsonPropertyName("file")] public required string File { get; init; }
}
