using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Tomlyn.Model;

namespace MMM.Forge;

public enum ForgeVersionRangeEnd
{
    Closed,
    Open,
}

public readonly struct ForgeVersionRange : IVersionRange
{
    readonly string _original;
    [MemberNotNullWhen(true, nameof(Start))] public bool IsSingle { get; }
    public SemanticVersion? Start { get; }
    public SemanticVersion? End { get; }
    public ForgeVersionRangeEnd StartType { get; }
    public ForgeVersionRangeEnd EndType { get; }

    public ForgeVersionRange(ReadOnlySpan<char> value)
    {
        _original = value.ToString();
        value = value[1..^1];
        int i = value.IndexOf(',');
        if (i == -1)
        {
            Start = SemanticVersion.Parse(value.Trim());
            IsSingle = true;
            StartType = ForgeVersionRangeEnd.Closed;
            EndType = ForgeVersionRangeEnd.Closed;
        }
        else
        {
            ReadOnlySpan<char> start = value[..i].Trim();
            ReadOnlySpan<char> end = value[(i + 1)..].Trim();

            Start = start.IsEmpty ? null : SemanticVersion.Parse(start);
            End = end.IsEmpty ? null : SemanticVersion.Parse(end);

            StartType = start.IsEmpty ? ForgeVersionRangeEnd.Open : ForgeVersionRangeEnd.Closed;
            EndType = end.IsEmpty ? ForgeVersionRangeEnd.Open : ForgeVersionRangeEnd.Closed;
        }
    }

    public bool Satisfies(string version) =>
        SemanticVersion.TryParse(version, out SemanticVersion sversion) &&
        Satisfies(sversion);

    public bool Satisfies(SemanticVersion version)
    {
        if (IsSingle) return version == Start;

        return (StartType, EndType) switch
        {
            (ForgeVersionRangeEnd.Open, ForgeVersionRangeEnd.Open) => true,
            (ForgeVersionRangeEnd.Open, ForgeVersionRangeEnd.Closed) => version <= End!.Value,
            (ForgeVersionRangeEnd.Closed, ForgeVersionRangeEnd.Open) => version >= Start!.Value,
            (ForgeVersionRangeEnd.Closed, ForgeVersionRangeEnd.Closed) => version >= Start!.Value && version <= End!.Value,
            _ => throw new UnreachableException()
        };
    }

    public override string ToString() => _original;
}

public class ForgeModDependency(TomlTable value)
{
    public string ModId { get; } = (string)value["modId"];
    public bool Mandatory { get; } = (bool)value["mandatory"];
    public ForgeVersionRange VersionRange { get; } = new ForgeVersionRange((string)value["versionRange"]);
}

public class ForgeMod : IMod
{
    public string Id { get; }
    public string Version { get; }
    public string Name { get; }
    public string Description { get; }
    public string Authors { get; }
    public ImmutableArray<ForgeModDependency> Dependencies { get; }

    public ForgeMod(TomlTable value, TomlTable? dependencies)
    {
        Id = (string)value["modId"];
        Version = (string)value["version"];
        Name = (string)value["displayName"];
        Description = (string)value["description"];
        Authors = (string)value["authors"];
        Dependencies = dependencies is null ? [] : dependencies.TryGetValue(Id, out object? v) ? [.. ((TomlTableArray)v).Select(v => new ForgeModDependency(v))] : [];
    }
}
