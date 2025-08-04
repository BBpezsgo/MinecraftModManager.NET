namespace MMM;

public readonly struct DependencyError
{
    public required DependencyErrorLevel Level { get; init; }
    public required InstalledMod Subject { get; init; }
    public required string OtherId { get; init; }
    public required InstalledComponent? OtherInstalled { get; init; }
}
