using System.Diagnostics;

namespace MMM;

public static class Java
{
    public static string? GetVersion()
    {
        try
        {
            Process? process = Process.Start(new ProcessStartInfo()
            {
                FileName = "java",
                Arguments = "--version",
                RedirectStandardOutput = true,
            });
            if (process is null) return null;

            process.WaitForExit();

            string stdOutput = process.StandardOutput.ReadToEnd();
            int i = stdOutput.IndexOf(' ') + 1;
            int j = stdOutput.IndexOf(' ', i);
            return stdOutput[i..j];
        }
        catch
        {
            return null;
        }
    }
}
