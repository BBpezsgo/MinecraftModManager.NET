using System.Text.Json.Serialization;

namespace MMM;

public class ModlistLockEntry
{
    [JsonPropertyName("id")] public required string Id { get; set; }
    [JsonPropertyName("downloadUrl")] public required string DownloadUrl { get; set; }
    [JsonPropertyName("fileName")] public required string FileName { get; set; }
    [JsonPropertyName("hash")] public required string Hash { get; set; }
    [JsonPropertyName("releasedOn")] public required string ReleasedOn { get; set; }

    public override string ToString() => $"{FileName ?? Id}";
}
