using System.Numerics;

namespace MMM;

readonly struct LogEntry(string text) : IAdditionOperators<LogEntry, LogEntry, LogEntry>, IEquatable<LogEntry>
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

    public bool Equals(LogEntry other) => string.Equals(_text, other._text);

    public static implicit operator string(LogEntry v) => v._text ?? string.Empty;
    public static implicit operator LogEntry(string? v) => new(v ?? string.Empty);
}

static partial class Log
{
    static LogEntry LastLogEntry = default;

    public static void Keep(LogEntry logEntry)
    {
        if (!LastLogEntry.Equals(default)) throw new InvalidOperationException($"Last log entry already preserved.");
        LastLogEntry = logEntry;
    }

    public static void Rekeep(LogEntry logEntry)
    {
        LastLogEntry = logEntry;
    }

    public static void Unkeep()
    {
        LastLogEntry = default;
    }

    public static LogEntry Write(string? text)
    {
        if (string.IsNullOrEmpty(text)) return default;
        Console.Write(text);
        return new LogEntry(text);
    }

    public static void Section(string title)
    {
        LastLogEntry.Clear();

        Console.WriteLine();

        Console.Write("\e[1m");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(":: ");

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(title);

        Console.Write("\e[22m");
        Console.ResetColor();

        Console.Write(LastLogEntry);
    }

    public static void MajorAction(string text)
    {
        LastLogEntry.Clear();

        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("  ==> ");
        Console.WriteLine(text);
        Console.ResetColor();

        Console.Write(LastLogEntry);
    }

    public static void MinorAction(string text)
    {
        LastLogEntry.Clear();

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("   -> ");
        Console.WriteLine(text);
        Console.ResetColor();

        Console.Write(LastLogEntry);
    }

    public static void Info(string text)
    {
        LastLogEntry.Clear();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(">>> ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(text);
        Console.ResetColor();

        Console.Write(LastLogEntry);
    }

    public static void None(string text)
    {
        LastLogEntry.Clear();

        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("    ");
        Console.WriteLine(text);
        Console.ResetColor();

        Console.Write(LastLogEntry);
    }

    public static void Warning(string text)
    {
        LastLogEntry.Clear();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("\e[1m");
        Console.Write(">>> WARNING: ");
        Console.Write("\e[22m");

        Console.WriteLine(text);
        Console.ResetColor();

        Console.Write(LastLogEntry);
    }

    public static void Error(string text)
    {
        LastLogEntry.Clear();

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("\e[1m");
        Console.Write(">>> ERROR: ");
        Console.Write("\e[22m");

        Console.WriteLine(text);
        Console.ResetColor();

        Console.Write(LastLogEntry);
    }

    static void ErrorImpl(Exception? exception)
    {
        if (exception is null) return;

        static void _(Exception e, int depth)
        {
            Console.Write(new string(' ', depth * 2));
            if (e is ApplicationException appE)
            {
                Console.Write(e.Message);
                Console.WriteLine();
            }
            else
            {
#if DEBUG
                Console.Write(e.GetType().Name);
                Console.Write(' ');
#endif
                Console.Write(e.Message);
                Console.WriteLine();
#if DEBUG
                foreach (string item in e.StackTrace?.Split('\n') ?? [])
                {
                    Console.Write(new string(' ', depth * 2 + 2));
                    Console.WriteLine(item.Trim());
                }
#endif
            }
        }

        int depth = 0;

        do
        {
            if (exception is AggregateException aggregateException)
            {
                aggregateException = aggregateException.Flatten();
                if (aggregateException.InnerExceptions.Count == 0)
                {
                    _(aggregateException, depth++);
                }
                else if (aggregateException.InnerExceptions.Count == 1)
                {
                    _(aggregateException.InnerExceptions[0], depth++);
                }
                else
                {
                    foreach (var item in aggregateException.InnerExceptions)
                    {
                        _(item, depth++);
                    }
                }
                exception = null;
            }
            else if (exception is ApplicationException applicationException)
            {
                _(applicationException, depth++);
                exception = null;
            }
            else
            {
                _(exception, depth++);
                exception = exception?.InnerException;
            }
        }
        while (exception is not null);
    }

    public static void Error(Exception exception)
    {
        LastLogEntry.Clear();

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("\e[1m");
        Console.Write(">>> ERROR: ");
        Console.Write("\e[22m");

        ErrorImpl(exception);
        Console.ResetColor();

        Console.Write(LastLogEntry);
    }

    public static void Debug(object? text) => Debug(text?.ToString());
    public static void Debug(string? text)
    {
        LastLogEntry.Clear();

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("    ");
        Console.WriteLine(text);
        Console.ResetColor();

        Console.Write(LastLogEntry);
    }

    public static void NewLine(int count = 1)
    {
        LastLogEntry.Clear();

        for (int i = 0; i < count; i++) Console.WriteLine();

        Console.Write(LastLogEntry);
    }
}
