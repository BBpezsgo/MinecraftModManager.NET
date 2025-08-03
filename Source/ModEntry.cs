using System.Text.Json.Serialization;

namespace MMM;

public class ModEntry
{
    [JsonPropertyName("id")] public required string Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }

    public override string ToString() => $"{Name ?? Id}";
}
