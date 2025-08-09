namespace MMM;

public class ModUninstallInfo
{
    public required ModUninstallReason Reason { get; init; }
    public required string? File { get; init; }
    public required ModLock LockEntry { get; init; }

    public override string ToString() => $"{LockEntry?.Name ?? Path.GetFileName(File)}";
}
