using System.Numerics;

namespace MMM;

public readonly struct LogEntry(string text) : IAdditionOperators<LogEntry, LogEntry, LogEntry>, IEquatable<LogEntry>
{
    readonly string _text = text;

    public void Clear()
    {
        if (string.IsNullOrEmpty(_text)) return;
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine();
        }
        else
        {
            string b = new('\b', _text.Length);
            Console.Write(b);
            Console.Write(new string(' ', _text.Length));
            Console.Write(b);
        }
    }

    public void Back()
    {
        if (string.IsNullOrEmpty(_text)) return;
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine();
        }
        else
        {
            string b = new('\b', _text.Length);
            Console.Write(b);
        }
    }

    public static LogEntry operator +(LogEntry left, LogEntry right) => new((left._text ?? string.Empty) + (right._text ?? string.Empty));

    public override string ToString() => _text;

    public bool Equals(LogEntry other) => string.Equals(_text, other._text, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is LogEntry other && Equals(other);

    public static implicit operator string(LogEntry v) => v._text ?? string.Empty;
    public static implicit operator LogEntry(string? v) => new(v ?? string.Empty);

    public override int GetHashCode() => _text.GetHashCode();

    public static bool operator ==(LogEntry left, LogEntry right) => left.Equals(right);
    public static bool operator !=(LogEntry left, LogEntry right) => !left.Equals(right);
}
