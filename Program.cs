#pragma warning disable CS0162
#pragma warning disable IDE0060

using System.Diagnostics;
using System.Runtime.InteropServices;
using Logger;

namespace MMM;

public static partial class Program
{
    public static void Main(string[] args)
    {
        Windows.EnableVirtualTerminalProcessing();

        CancellationTokenSource cts = new();
        Console.CancelKeyPress += (sender, e) =>
        {
            cts.Cancel();
            e.Cancel = true;
        };

        using Task task = Run(args, cts.Token);

        while (!task.IsCompleted)
        {
            Thread.Yield();
        }

        if (task.Exception is not null)
        {
            Log.Error(task.Exception);
        }
    }

    static async Task Run(string[] args, CancellationToken ct)
    {
        string? action = args.Length > 0 && !args[0].StartsWith('-') ? args[0] : null;
        if (action is not null) args = args[1..];

        if (action is null && (args.Contains("-h") || args.Contains("--help") || args.Length == 0))
        {
            Help.PrintHelp(null);
            return;
        }

        if (!File.Exists(Context.ModlistPath))
        {
            Log.Warning($"No package.json found in the current folder. ({Path.GetFullPath(Context.ModlistPath)})");
            if (!Log.AskYesNo("Do you want to install it?")) return;

            Actions.Install.PerformInstall();
        }

        switch (action)
        {
            case "update":
                if (args.ContainsAny("--help", "-h"))
                {
                    Help.PrintHelp(action);
                    break;
                }

                if (args.Length != 0)
                {
                    throw new ApplicationArgumentsException($"Wrong number of arguments passed");
                }

                await Actions.Update.PerformUpdate(ct);
                break;
            case "check":
                if (args.ContainsAny("--help", "-h"))
                {
                    Help.PrintHelp(action);
                    break;
                }

                if (args.Length != 0)
                {
                    throw new ApplicationArgumentsException($"Wrong number of arguments passed");
                }

                await Actions.Check.PerformCheck(ct);
                break;
            case "add":
                if (args.ContainsAny("--help", "-h"))
                {
                    Help.PrintHelp(action);
                    break;
                }

                if (args.Length < 1)
                {
                    throw new ApplicationArgumentsException($"Wrong number of arguments passed");
                }

                await Actions.Add.PerformAdd(args, ct);
                break;
            case "remove":
                if (args.ContainsAny("--help", "-h"))
                {
                    Help.PrintHelp(action);
                    break;
                }

                if (args.Length < 1)
                {
                    throw new ApplicationArgumentsException($"Wrong number of arguments passed");
                }

                await Actions.Remove.PerformRemove(args, ct);
                break;
            case "list":
                if (args.ContainsAny("--help", "-h"))
                {
                    Help.PrintHelp(action);
                    break;
                }

                if (args.Length != 0)
                {
                    throw new ApplicationArgumentsException($"Wrong number of arguments passed");
                }

                await Actions.List.PerformList(ct);
                break;
            case "change":
                if (args.ContainsAny("--help", "-h"))
                {
                    Help.PrintHelp(action);
                    break;
                }

                if (args.Length != 1)
                {
                    throw new ApplicationArgumentsException($"Wrong number of arguments passed");
                }

                await Actions.Change.PerformChange(args[0], ct);
                break;
            default:
                throw new ApplicationArgumentsException($"Invalid action {action}");
        }
    }
}
