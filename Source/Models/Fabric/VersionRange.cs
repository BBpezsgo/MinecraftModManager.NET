using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MMM.Fabric;

[JsonConverter(typeof(FabricVersionRangeJsonConverter))]
public abstract class FabricVersionRange : IVersionRange
{
    public abstract bool Satisfies(string version);
    public abstract bool Satisfies(SemanticVersion version);
}

public class FabricVersionRangeJsonConverter : JsonConverter<FabricVersionRange>
{
    public override FabricVersionRange Read(
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
                return new FabricListedVersionRange(FabricVersionListOperator.And, [.. s.Split(' ').Select(v => new FabricSingleVersionRange(v))]);
            }
            else
            {
                return new FabricSingleVersionRange(s);
            }
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            ImmutableArray<FabricSingleVersionRange>.Builder versions = ImmutableArray.CreateBuilder<FabricSingleVersionRange>();
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
                versions.Add(new FabricSingleVersionRange(reader.GetString()!));
                reader.Read();
            }
            return new FabricListedVersionRange(FabricVersionListOperator.Or, versions.ToImmutable());
        }
        else
        {
            throw new JsonException("Expected a string or an array");
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        FabricVersionRange value,
        JsonSerializerOptions options)
    {
        switch (value)
        {
            case FabricSingleVersionRange singleVersion:
                writer.WriteStringValue(singleVersion.ToString());
                break;
            case FabricListedVersionRange listedVersion:
                writer.WriteStartArray();
                foreach (FabricSingleVersionRange item in listedVersion.Values)
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

public enum FabricVersionOperator
{
    Equal,
    GreaterEqual,
    LessEqual,
    Greater,
    Less,
    UptoMajor,
    UptoMinor,
}

public class FabricSingleVersionRange : FabricVersionRange, IEquatable<FabricSingleVersionRange>
{
    public FabricVersionOperator Operator { get; }
    public PartialSemanticVersion? Version { get; }
    public string Raw { get; }

    public FabricSingleVersionRange(string value)
    {
        //if (value.Contains('-')) throw new NotSupportedException($"Range versions not supported");

        if (value.StartsWith('='))
        {
            Operator = FabricVersionOperator.Equal;
            Raw = value = value[1..];
        }
        else if (value.StartsWith(">=", StringComparison.Ordinal))
        {
            Operator = FabricVersionOperator.GreaterEqual;
            Raw = value = value[2..];
        }
        else if (value.StartsWith("<=", StringComparison.Ordinal))
        {
            Operator = FabricVersionOperator.LessEqual;
            Raw = value = value[2..];
        }
        else if (value.StartsWith('>'))
        {
            Operator = FabricVersionOperator.Greater;
            Raw = value = value[1..];
        }
        else if (value.StartsWith('<'))
        {
            Operator = FabricVersionOperator.Less;
            Raw = value = value[1..];
        }
        else if (value.StartsWith('^'))
        {
            Operator = FabricVersionOperator.UptoMajor;
            Raw = value = value[1..];
        }
        else if (value.StartsWith('~'))
        {
            Operator = FabricVersionOperator.UptoMinor;
            Raw = value = value[1..];
        }
        else
        {
            Operator = FabricVersionOperator.Equal;
            Raw = value;
        }

        Version = PartialSemanticVersion.TryParse(value, out PartialSemanticVersion v) ? v : null;
    }

    public override string ToString() => Raw == "*" ? Raw : $"{Operator switch
    {
        FabricVersionOperator.Equal => "=",
        FabricVersionOperator.GreaterEqual => ">=",
        FabricVersionOperator.LessEqual => "<=",
        FabricVersionOperator.Greater => ">",
        FabricVersionOperator.Less => "<",
        FabricVersionOperator.UptoMajor => "^",
        FabricVersionOperator.UptoMinor => "~",
        _ => null,
    }}{Raw}";
    public bool Equals(FabricSingleVersionRange? other) => other is not null && Raw == other.Raw;
    public override int GetHashCode() => Raw.GetHashCode();
    public override bool Equals(object? obj) => obj is FabricSingleVersionRange other && Equals(other);

    public override bool Satisfies(SemanticVersion version)
    {
        if (Raw == "*") return true;
        if (!Version.HasValue) return false;
        switch (Operator)
        {
            case FabricVersionOperator.Equal:
                return version == Version.Value;
            case FabricVersionOperator.GreaterEqual:
                return version >= Version.Value;
            case FabricVersionOperator.LessEqual:
                return version <= Version.Value;
            case FabricVersionOperator.Greater:
                return version > Version.Value;
            case FabricVersionOperator.Less:
                return version < Version.Value;
            case FabricVersionOperator.UptoMajor:
                if (version >= Version.Value) return true;
                if (version < new PartialSemanticVersion(Version.Value.Major.HasValue ? (Version.Value.Major.Value + 1) : null, 0, 0)) return true;
                return true;
            case FabricVersionOperator.UptoMinor:
                if (version >= Version.Value) return true;
                if (version < new PartialSemanticVersion(Version.Value.Major, Version.Value.Minor.HasValue ? (Version.Value.Minor.Value + 1) : null, 0)) return true;
                return true;
            default:
                throw new UnreachableException();
        }
    }
    public override bool Satisfies(string version)
    {
        if (Raw == "*") return true;
        if (!Version.HasValue) return false;
        if (!SemanticVersion.TryParse(version, out SemanticVersion sversion)) return false;
        return Satisfies(sversion);
    }
}

public enum FabricVersionListOperator
{
    And,
    Or,
}

public class FabricListedVersionRange(FabricVersionListOperator @operator, ImmutableArray<FabricSingleVersionRange> values) : FabricVersionRange, IEquatable<FabricListedVersionRange>
{
    public FabricVersionListOperator Operator { get; } = @operator;
    public ImmutableArray<FabricSingleVersionRange> Values { get; } = values;

    public override string ToString() => $"[ {string.Join(" ", Values.Select(v => v.ToString()))} ]";
    public bool Equals(FabricListedVersionRange? other)
    {
        if (other is null) return false;
        return new HashSet<FabricSingleVersionRange>(Values).SetEquals(other.Values);
    }
    public override int GetHashCode() => Values.GetHashCode();
    public override bool Equals(object? obj) => obj is FabricListedVersionRange other && Equals(other);

    public override bool Satisfies(string version) => Operator switch
    {
        FabricVersionListOperator.Or => Values.Any(v => v.Satisfies(version)),
        FabricVersionListOperator.And => Values.All(v => v.Satisfies(version)),
        _ => throw new UnreachableException()
    };

    public override bool Satisfies(SemanticVersion version) => Operator switch
    {
        FabricVersionListOperator.Or => Values.Any(v => v.Satisfies(version)),
        FabricVersionListOperator.And => Values.All(v => v.Satisfies(version)),
        _ => throw new UnreachableException()
    };
}
