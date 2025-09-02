namespace MMM;

public class ModUninstallInfo
{
    public required ModUninstallReason Reason { get; init; }
    public required string? File { get; init; }
    public required ModLock? LockEntry { get; init; }
    public required string Name { get; init; }

    public override string ToString() => $"{Name ?? LockEntry?.Name ?? Path.GetFileName(File)}";
}
