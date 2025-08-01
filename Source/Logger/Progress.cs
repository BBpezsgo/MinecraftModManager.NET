using System.Text;

namespace MMM;

public class ProgressBar : IDisposable, IProgress<float>
{
    const int blockCount = 10;
    static readonly TimeSpan animationInterval = TimeSpan.FromSeconds(1.0 / 8);
    const string animation = @"|/-\";

    readonly Timer timer;

    float currentProgress = 0;
    string currentText = string.Empty;
    bool disposed = false;
    int animationIndex = 0;

    public ProgressBar()
    {
        timer = new Timer(TimerHandler);
        if (Console.IsOutputRedirected) return;
        ResetTimer();
    }

    public void Report(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        Interlocked.Exchange(ref currentProgress, value);
    }

    public void Report(int index, int length) => Report((float)index / (float)length);

    void TimerHandler(object? state)
    {
        lock (timer)
        {
            if (disposed) return;

            int progressBlockCount = (int)(currentProgress * blockCount);
            string text = string.Format("[{0}{1}] {2}",
                new string('#', progressBlockCount), new string('-', blockCount - progressBlockCount),
                animation[animationIndex++ % animation.Length]);
            UpdateText(text);

            ResetTimer();
        }
    }

    void UpdateText(string text)
    {
        // Get length of common portion
        int commonPrefixLength = 0;
        int commonLength = Math.Min(currentText.Length, text.Length);
        while (commonPrefixLength < commonLength && text[commonPrefixLength] == currentText[commonPrefixLength])
        {
            commonPrefixLength++;
        }

        // Backtrack to the first differing character
        StringBuilder outputBuilder = new();
        outputBuilder.Append('\b', currentText.Length - commonPrefixLength);

        // Output new suffix
        outputBuilder.Append(text[commonPrefixLength..]);

        // If the new text is shorter than the old one: delete overlapping characters
        int overlapCount = currentText.Length - text.Length;
        if (overlapCount > 0)
        {
            outputBuilder.Append(' ', overlapCount);
            outputBuilder.Append('\b', overlapCount);
        }

        Console.Write(outputBuilder);
        currentText = text;
    }

    void ResetTimer()
    {
        timer.Change(animationInterval, TimeSpan.FromMilliseconds(-1));
    }

    public void Dispose()
    {
        lock (timer)
        {
            disposed = true;
            UpdateText(string.Empty);
        }
    }

}

static partial class Log
{
    public static LogEntry Progress(int index, int length) => Progress((float)index / (float)length);
    public static LogEntry Progress(float progress)
    {
        int w = Console.WindowWidth - Console.CursorLeft - 2;
        if (w < 2) return default;

        int fill = (int)(w * progress);
        int empty = w - fill;

        StringBuilder b = new();
        b.Append('[');
        b.Append('#', fill);
        b.Append(' ', empty);
        b.Append(']');

        return Write(b.ToString());
    }
}
