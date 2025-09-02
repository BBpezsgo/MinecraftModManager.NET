#pragma warning disable CS0162
#pragma warning disable IDE0060

namespace MMM;

public static class Program
{
    public static void Main(string[] args)
    {
        CancellationTokenSource cts = new();
        Console.CancelKeyPress += (sender, e) =>
        {
            cts.Cancel();
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
        if (args.Length == 0)
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

        switch (args[0])
        {
            case "update":
                if (args.ContainsAny("--help", "-h"))
                {
                    Help.PrintHelp(args[0]);
                    break;
                }

                if (args.Length != 1)
                {
                    throw new ApplicationArgumentsException($"Wrong number of arguments passed");
                }

                await Actions.Update.PerformUpdate(ct);
                break;
            case "check":
                if (args.ContainsAny("--help", "-h"))
                {
                    Help.PrintHelp(args[0]);
                    break;
                }

                if (args.Length != 1)
                {
                    throw new ApplicationArgumentsException($"Wrong number of arguments passed");
                }

                await Actions.Check.PerformCheck(ct);
                break;
            case "add":
                if (args.ContainsAny("--help", "-h"))
                {
                    Help.PrintHelp(args[0]);
                    break;
                }

                if (args.Length < 2)
                {
                    throw new ApplicationArgumentsException($"Wrong number of arguments passed");
                }

                await Actions.Add.PerformAdd(args[1..], ct);
                break;
            case "remove":
                if (args.ContainsAny("--help", "-h"))
                {
                    Help.PrintHelp(args[0]);
                    break;
                }

                if (args.Length < 2)
                {
                    throw new ApplicationArgumentsException($"Wrong number of arguments passed");
                }

                await Actions.Remove.PerformRemove(args[1..], ct);
                break;
            case "list":
                if (args.ContainsAny("--help", "-h"))
                {
                    Help.PrintHelp(args[0]);
                    break;
                }

                if (args.Length != 1)
                {
                    throw new ApplicationArgumentsException($"Wrong number of arguments passed");
                }

                await Actions.List.PerformList(ct);
                break;
            case "change":
                if (args.ContainsAny("--help", "-h"))
                {
                    Help.PrintHelp(args[0]);
                    break;
                }

                if (args.Length != 2)
                {
                    throw new ApplicationArgumentsException($"Wrong number of arguments passed");
                }

                await Actions.Change.PerformChange(args[1], ct);
                break;
            default:
                if (args.ContainsAny("--help", "-h"))
                {
                    Help.PrintHelp(null);
                    break;
                }

                throw new ApplicationArgumentsException($"Invalid action {args[0]}");
        }
    }
}
