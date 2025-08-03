using System.Numerics;

namespace MMM;

public readonly struct SemanticVersion(int major, int minor, int patch) : IComparisonOperators<SemanticVersion, SemanticVersion, bool>, IEquatable<SemanticVersion>
{
    public int Major { get; } = major;
    public int Minor { get; } = minor;
    public int Patch { get; } = patch;

    public static bool operator ==(SemanticVersion left, SemanticVersion right) => left.Equals(right);
    public static bool operator !=(SemanticVersion left, SemanticVersion right) => !left.Equals(right);

    public static bool operator <(SemanticVersion left, SemanticVersion right)
    {
        if (left.Major < right.Major) return true;
        if (left.Major > right.Major) return false;

        if (left.Minor < right.Minor) return true;
        if (left.Minor > right.Minor) return false;

        if (left.Patch < right.Patch) return true;
        if (left.Patch > right.Patch) return false;

        return false;
    }
    public static bool operator >(SemanticVersion left, SemanticVersion right)
    {
        if (left.Major > right.Major) return true;
        if (left.Major < right.Major) return false;

        if (left.Minor > right.Minor) return true;
        if (left.Minor < right.Minor) return false;

        if (left.Patch > right.Patch) return true;
        if (left.Patch < right.Patch) return false;

        return false;
    }

    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left == right || left < right;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left == right || left > right;

    public override bool Equals(object? obj) => obj is SemanticVersion other && Equals(other);
    public bool Equals(SemanticVersion other) => Major == other.Major && Minor == other.Minor && Patch == other.Patch;
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch);
    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    static readonly System.Buffers.SearchValues<char> s_09 = System.Buffers.SearchValues.Create("0123456789");
    static readonly System.Buffers.SearchValues<char> s_version = System.Buffers.SearchValues.Create("0123456789.");

    public static bool TryParse(ReadOnlySpan<char> value, out SemanticVersion version)
    {
        int major = 0, minor = 0, patch = 0;
        version = default;

        int i = value.IndexOfAnyExcept(s_version);
        if (i != -1) value = value[..i];

        Span<Range> ranges = stackalloc Range[4];
        ranges = ranges[..value.Split(ranges, '.')];

        if (ranges.Length == 0) return false;
        if (ranges.Length >= 1 && !int.TryParse(value[ranges[0]], System.Globalization.NumberStyles.None, null, out major)) return false;
        if (ranges.Length >= 2 && !int.TryParse(value[ranges[1]], System.Globalization.NumberStyles.None, null, out minor)) return false;
        if (ranges.Length >= 3 && !int.TryParse(value[ranges[2]], System.Globalization.NumberStyles.None, null, out patch)) return false;

        version = new SemanticVersion(major, minor, patch);
        return true;
    }
    public static SemanticVersion Parse(ReadOnlySpan<char> value)
    {
        int major = 0, minor = 0, patch = 0;

        int i = value.IndexOfAnyExcept(s_version);
        if (i != -1) value = value[..i];

        Span<Range> ranges = stackalloc Range[4];
        ranges = ranges[..value.Split(ranges, '.')];

        if (ranges.Length == 0) throw new FormatException();
        if (ranges.Length >= 1 && !int.TryParse(value[ranges[0]], System.Globalization.NumberStyles.None, null, out major)) throw new FormatException();
        if (ranges.Length >= 2 && !int.TryParse(value[ranges[1]], System.Globalization.NumberStyles.None, null, out minor)) throw new FormatException();
        if (ranges.Length >= 3 && !int.TryParse(value[ranges[2]], System.Globalization.NumberStyles.None, null, out patch)) throw new FormatException();

        return new SemanticVersion(major, minor, patch);
    }
}
