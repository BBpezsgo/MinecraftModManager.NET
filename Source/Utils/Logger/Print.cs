namespace MMM;

static partial class Log
{
    static LogEntry LastLogEntry = default;
    public static List<Lock> InteractiveLocks = [];

    readonly struct AutoScope : IDisposable
    {
        public void Dispose()
        {
            Console.Write(LastLogEntry);
            foreach (Lock item in InteractiveLocks)
            {
                item.Exit();
            }
        }
    }

    static AutoScope Auto()
    {
        foreach (Lock item in InteractiveLocks)
        {
            item.Enter();
        }
        LastLogEntry.Clear();
        return new AutoScope();
    }

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
        using AutoScope _ = Auto();

        Console.WriteLine();

        Console.Write("\e[1m");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(":: ");

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(title);

        Console.Write("\e[22m");
        Console.ResetColor();
    }

    public static void MajorAction(string text)
    {
        using AutoScope _ = Auto();

        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("  ==> ");
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void MinorAction(string text)
    {
        using AutoScope _ = Auto();

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("   -> ");
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void Info(string text)
    {
        using AutoScope _ = Auto();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(">>> ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void None(string text)
    {
        using AutoScope _ = Auto();

        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("    ");
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void Warning(string text)
    {
        using AutoScope _ = Auto();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("\e[1m");
        Console.Write(">>> WARNING: ");
        Console.Write("\e[22m");

        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void Error(string text)
    {
        using AutoScope _ = Auto();

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("\e[1m");
        Console.Write(">>> ERROR: ");
        Console.Write("\e[22m");

        Console.WriteLine(text);
        Console.ResetColor();
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
        using AutoScope _ = Auto();

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("\e[1m");
        Console.Write(">>> ERROR: ");
        Console.Write("\e[22m");

        ErrorImpl(exception);
        Console.ResetColor();
    }

    public static void Debug(object? text) => Debug(text?.ToString());
    public static void Debug(string? text)
    {
        using AutoScope _ = Auto();

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("    ");
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void NewLine(int count = 1)
    {
        using AutoScope _ = Auto();

        for (int i = 0; i < count; i++) Console.WriteLine();
    }
}
