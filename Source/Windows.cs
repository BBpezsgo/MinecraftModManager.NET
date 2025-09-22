using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MMM;

static partial class Windows
{
    const int STD_OUTPUT_HANDLE = -11;
    const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 4;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [Conditional("WIN")]
    public static void EnableVirtualTerminalProcessing()
    {
        IntPtr handle = GetStdHandle(STD_OUTPUT_HANDLE);

        if (!GetConsoleMode(handle, out uint mode))
        {
            Console.Error.WriteLine("Failed to get console mode");
            return;
        }

        mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
        if (!SetConsoleMode(handle, mode))
        {
            Console.Error.WriteLine("Failed to set console mode");
            return;
        }
    }
}
