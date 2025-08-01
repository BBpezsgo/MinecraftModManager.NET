using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MMM.Fabric;

[JsonConverter(typeof(VersionRangeJsonConverter))]
public abstract class VersionRange
{
    public abstract bool Satisfies(string version);
    public abstract bool Satisfies(SemanticVersion version);
}

public class VersionRangeJsonConverter : JsonConverter<VersionRange>
{
    public override VersionRange Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        reader.SkipJunk();
        if (reader.TokenType == JsonTokenType.String)
        {
            string s = reader.GetString()!;
            if (s.Contains(' '))
            {
                return new ListedVersionRange(VersionListOperator.And, [.. s.Split(' ').Select(v => new SingleVersionRange(v))]);
            }
            else
            {
                return new SingleVersionRange(s);
            }
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            ImmutableArray<SingleVersionRange>.Builder versions = ImmutableArray.CreateBuilder<SingleVersionRange>();
            while (reader.TokenType != JsonTokenType.EndArray)
            {
                reader.SkipJunk();
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    reader.Read();
                    break;
                }
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException("Expected a string");
                }
                versions.Add(new SingleVersionRange(reader.GetString()!));
                reader.Read();
            }
            return new ListedVersionRange(VersionListOperator.Or, versions.ToImmutable());
        }
        else
        {
            throw new JsonException("Expected a string or an array");
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        VersionRange value,
        JsonSerializerOptions options)
    {
        switch (value)
        {
            case SingleVersionRange singleVersion:
                writer.WriteStringValue(singleVersion.ToString());
                break;
            case ListedVersionRange listedVersion:
                writer.WriteStartArray();
                foreach (SingleVersionRange item in listedVersion.Values)
                {
                    writer.WriteStringValue(item.ToString());
                }
                writer.WriteEndArray();
                break;
            default:
                throw new UnreachableException();
        }
    }
}

public enum VersionOperator
{
    Equal,
    GreaterEqual,
    LessEqual,
    Greater,
    Less,
    UptoMajor,
    UptoMinor,
}

public class SingleVersionRange : VersionRange, IEquatable<SingleVersionRange>
{
    public readonly VersionOperator Operator;
    public readonly SemanticVersion? Version;
    public readonly string Raw;

    public SingleVersionRange(string value)
    {
        //if (value.Contains('-')) throw new NotSupportedException($"Range versions not supported");

        if (value.StartsWith('='))
        {
            Operator = VersionOperator.Equal;
            Raw = value[1..];
        }
        else if (value.StartsWith(">="))
        {
            Operator = VersionOperator.GreaterEqual;
            Raw = value[2..];
        }
        else if (value.StartsWith("<="))
        {
            Operator = VersionOperator.LessEqual;
            Raw = value[2..];
        }
        else if (value.StartsWith('>'))
        {
            Operator = VersionOperator.Greater;
            Raw = value[1..];
        }
        else if (value.StartsWith('<'))
        {
            Operator = VersionOperator.Less;
            Raw = value[1..];
        }
        else if (value.StartsWith('^'))
        {
            Operator = VersionOperator.UptoMajor;
            Raw = value[1..];
        }
        else if (value.StartsWith('~'))
        {
            Operator = VersionOperator.UptoMinor;
            Raw = value[1..];
        }
        else
        {
            Operator = VersionOperator.Equal;
            Raw = value;
        }

        Version = SemanticVersion.TryParse(Raw, out SemanticVersion v) ? v : null;
    }

    public override string ToString() => Raw == "*" ? Raw : $"{Operator switch
    {
        VersionOperator.Equal => "=",
        VersionOperator.GreaterEqual => ">=",
        VersionOperator.LessEqual => "<=",
        VersionOperator.Greater => ">",
        VersionOperator.Less => "<",
        VersionOperator.UptoMajor => "^",
        VersionOperator.UptoMinor => "~",
        _ => null,
    }}{Raw}";
    public bool Equals(SingleVersionRange? other) => other is not null && Raw == other.Raw;
    public override int GetHashCode() => Raw.GetHashCode();
    public override bool Equals(object? obj) => obj is SingleVersionRange other && Equals(other);

    public override bool Satisfies(SemanticVersion version)
    {
        if (Raw == "*") return true;
        if (!Version.HasValue) return false;
        switch (Operator)
        {
            case VersionOperator.Equal:
                return version == Version.Value;
            case VersionOperator.GreaterEqual:
                return version >= Version.Value;
            case VersionOperator.LessEqual:
                return version <= Version.Value;
            case VersionOperator.Greater:
                return version > Version.Value;
            case VersionOperator.Less:
                return version < Version.Value;
            case VersionOperator.UptoMajor:
                if (version >= Version.Value) return true;
                if (version < new SemanticVersion(Version.Value.Major + 1, 0, 0)) return true;
                return true;
            case VersionOperator.UptoMinor:
                if (version >= Version.Value) return true;
                if (version < new SemanticVersion(Version.Value.Major, Version.Value.Minor + 1, 0)) return true;
                return true;
            default:
                throw new UnreachableException();
        }
    }
    public override bool Satisfies(string version)
    {
        if (Raw == "*") return true;
        if (!Version.HasValue) return false;
        if (!SemanticVersion.TryParse(version, out var sversion)) return false;
        return Satisfies(sversion);
    }
}

public enum VersionListOperator
{
    And,
    Or,
}

public class ListedVersionRange(VersionListOperator @operator, ImmutableArray<SingleVersionRange> values) : VersionRange, IEquatable<ListedVersionRange>
{
    public readonly VersionListOperator Operator = @operator;
    public readonly ImmutableArray<SingleVersionRange> Values = values;

    public override string ToString() => $"[ {string.Join(" ", Values.Select(v => v.ToString()))} ]";
    public bool Equals(ListedVersionRange? other)
    {
        if (other is null) return false;
        return new HashSet<SingleVersionRange>(Values).SetEquals(other.Values);
    }
    public override int GetHashCode() => Values.GetHashCode();
    public override bool Equals(object? obj) => obj is ListedVersionRange other && Equals(other);

    public override bool Satisfies(string version) => Operator switch
    {
        VersionListOperator.Or => Values.Any(v => v.Satisfies(version)),
        VersionListOperator.And => Values.All(v => v.Satisfies(version)),
        _ => throw new UnreachableException()
    };

    public override bool Satisfies(SemanticVersion version) => Operator switch
    {
        VersionListOperator.Or => Values.Any(v => v.Satisfies(version)),
        VersionListOperator.And => Values.All(v => v.Satisfies(version)),
        _ => throw new UnreachableException()
    };
}
