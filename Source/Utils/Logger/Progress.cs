using System.Text;

namespace MMM;

public class ProgressBar : IDisposable, IProgress<float>
{
    static readonly TimeSpan AnimationInterval = TimeSpan.FromSeconds(1.0 / 8);

    readonly Timer Timer;
    readonly Lock Lock;

    float Progress = 0;
    string Title = string.Empty;

    bool IsDisposed = false;
    LogEntry LastLine = default;

    public ProgressBar()
    {
        Timer = new Timer(TimerHandler);
        Lock = new Lock();
        Log.InteractiveLocks.Add(Lock);
        if (Console.IsOutputRedirected) return;

        Log.Keep(LastLine);

        ResetTimer();
    }

    public void Report(float value)
    {
        value = Math.Clamp(value, 0f, 1f);

        Interlocked.Exchange(ref Progress, value);
    }

    public void Report(int index, int length) => Report((float)index / (float)length);

    public void Report(string title, float value)
    {
        value = Math.Clamp(value, 0f, 1f);

        Interlocked.Exchange(ref Title, title);
        Interlocked.Exchange(ref Progress, value);
    }

    public void Report(string title, int index, int length) => Report(title, (float)index / (float)length);

    void TimerHandler(object? state)
    {
        lock (Lock)
        {
            if (IsDisposed) return;

            string title = Title;

            if (title.Length >= Console.WindowWidth / 2)
            {
                title = title[..(Console.WindowWidth / 2 - 3)] + "...";
            }

            LastLine.Back();
            LastLine = default;

            LastLine += Log.Write(title);
            LastLine += Log.Write(new string(' ', Math.Max(0, Console.WindowWidth / 2 - title.Length)));
            int w = Console.WindowWidth - Console.CursorLeft - 2;
            if (w >= 2)
            {
                int fill = (int)(w * Progress);
                int empty = w - fill;

                StringBuilder b = new();
                b.Append('[');
                b.Append('#', fill);
                b.Append(' ', empty);
                b.Append(']');

                LastLine += Log.Write(b.ToString());
            }

            Log.Rekeep(LastLine);

            ResetTimer();
        }
    }

    void ResetTimer()
    {
        Timer.Change(AnimationInterval, TimeSpan.FromMilliseconds(-1));
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        lock (Lock)
        {
            IsDisposed = true;
            LastLine.Clear();
            Log.Unkeep();
        }

        Log.InteractiveLocks.Remove(Lock);

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}
