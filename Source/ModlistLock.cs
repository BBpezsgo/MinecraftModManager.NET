using System.Text.Json.Serialization;

namespace MMM;

public class ModlistLockEntry
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("downloadUrl")] public required string DownloadUrl { get; set; }
    [JsonPropertyName("fileName")] public required string FileName { get; set; }
    [JsonPropertyName("hash")] public required string Hash { get; set; }
    [JsonPropertyName("releasedOn")] public required string ReleasedOn { get; set; }
    [JsonPropertyName("dependencies")] public required List<string> Dependencies { get; set; }

    public override string ToString() => $"{FileName ?? Id}";
}
