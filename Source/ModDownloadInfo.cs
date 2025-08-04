namespace MMM;

public class ModDownloadInfo
{
    public required ModUpdateReason Reason { get; init; }
    public required Modrinth.Models.Version Version { get; init; }
    public required Modrinth.Models.File File { get; init; }
    public required ModLock? LockEntry { get; init; }
    public required string DownloadFileName { get; init; }
    public required ModEntry Mod { get; init; }

    public override string ToString() => $"{Mod.Name ?? DownloadFileName}";
}
