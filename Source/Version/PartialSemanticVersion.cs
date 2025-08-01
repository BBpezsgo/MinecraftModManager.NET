using System.Numerics;

namespace MMM;

public readonly struct PartialSemanticVersion(int? major, int? minor, int? patch) : IComparisonOperators<PartialSemanticVersion, PartialSemanticVersion, bool>, IEquatable<PartialSemanticVersion>
{
    public int? Major { get; } = major;
    public int? Minor { get; } = minor;
    public int? Patch { get; } = patch;

    public static bool operator ==(PartialSemanticVersion left, PartialSemanticVersion right)
    {
        if (left.Major.HasValue && right.Major.HasValue && left.Major.Value != right.Major.Value) return false;
        if (left.Minor.HasValue && right.Minor.HasValue && left.Minor.Value != right.Minor.Value) return false;
        if (left.Patch.HasValue && right.Patch.HasValue && left.Patch.Value != right.Patch.Value) return false;

        return true;
    }

    public static bool operator !=(PartialSemanticVersion left, PartialSemanticVersion right) => !(left == right);

    public static bool operator <(PartialSemanticVersion left, PartialSemanticVersion right)
    {
        if (left.Major.HasValue && right.Major.HasValue && left.Major.Value < right.Major.Value) return true;
        if (left.Major.HasValue && right.Major.HasValue && left.Major.Value > right.Major.Value) return false;

        if (left.Minor.HasValue && right.Minor.HasValue && left.Minor.Value < right.Minor.Value) return true;
        if (left.Minor.HasValue && right.Minor.HasValue && left.Minor.Value > right.Minor.Value) return false;

        if (left.Patch.HasValue && right.Patch.HasValue && left.Patch.Value < right.Patch.Value) return true;
        if (left.Patch.HasValue && right.Patch.HasValue && left.Patch.Value > right.Patch.Value) return false;

        return false;
    }
    public static bool operator >(PartialSemanticVersion left, PartialSemanticVersion right)
    {
        if (left.Major.HasValue && right.Major.HasValue && left.Major.Value > right.Major.Value) return true;
        if (left.Major.HasValue && right.Major.HasValue && left.Major.Value < right.Major.Value) return false;

        if (left.Minor.HasValue && right.Minor.HasValue && left.Minor.Value > right.Minor.Value) return true;
        if (left.Minor.HasValue && right.Minor.HasValue && left.Minor.Value < right.Minor.Value) return false;

        if (left.Patch.HasValue && right.Patch.HasValue && left.Patch.Value > right.Patch.Value) return true;
        if (left.Patch.HasValue && right.Patch.HasValue && left.Patch.Value < right.Patch.Value) return false;

        return false;
    }

    public static bool operator <=(PartialSemanticVersion left, PartialSemanticVersion right) => left == right || left < right;
    public static bool operator >=(PartialSemanticVersion left, PartialSemanticVersion right) => left == right || left > right;

    public override bool Equals(object? obj) => obj is PartialSemanticVersion other && Equals(other);
    public bool Equals(PartialSemanticVersion other) => Major == other.Major && Minor == other.Minor && Patch == other.Patch;
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch);
    public override string ToString() => $"{(Major.HasValue ? Major.Value.ToString() : "x")}.{(Minor.HasValue ? Minor.Value.ToString() : "x")}.{(Patch.HasValue ? Patch.Value.ToString() : "x")}";

    static readonly System.Buffers.SearchValues<char> s_version = System.Buffers.SearchValues.Create("0123456789.x");

    static bool TryParseComponent(ReadOnlySpan<char> value, out int? component)
    {
        if (value.SequenceEqual("x"))
        {
            component = null;
            return true;
        }

        if (int.TryParse(value, System.Globalization.NumberStyles.None, null, out int v))
        {
            component = v;
            return true;
        }

        component = default;
        return false;
    }

    public static bool TryParse(ReadOnlySpan<char> value, out PartialSemanticVersion version)
    {
        version = default;
        int? major = 0, minor = 0, patch = 0;

        int i = value.IndexOfAnyExcept(s_version);
        if (i != -1) value = value[..i];

        Span<Range> ranges = stackalloc Range[4];
        ranges = ranges[..value.Split(ranges, '.')];

        if (ranges.Length == 0) return false;
        if (ranges.Length >= 1 && !TryParseComponent(value[ranges[0]], out major)) return false;
        if (ranges.Length >= 2 && !TryParseComponent(value[ranges[1]], out minor)) return false;
        if (ranges.Length >= 3 && !TryParseComponent(value[ranges[2]], out patch)) return false;

        version = new(major, minor, patch);
        return true;
    }

    public static implicit operator PartialSemanticVersion(SemanticVersion v) => new(v.Major, v.Minor, v.Patch);
}
