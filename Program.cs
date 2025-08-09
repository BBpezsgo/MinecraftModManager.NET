namespace MMM;

public static class Program
{
    public static void Main(string[] args)
    {
        if (!File.Exists(Context.ModlistPath))
        {
            Log.Error($"No package.json found");
            return;
        }

        if (args.Length == 0)
        {
            Log.Error($"No arguments passed");
            return;
        }

        CancellationTokenSource cts = new();
        Console.CancelKeyPress += (sender, e) =>
        {
            cts.Cancel();
        };
        Task task;

        switch (args[0])
        {
            case "update":
                if (args.Length != 1)
                {
                    Log.Error($"Wrong number of arguments passed");
                    return;
                }

                task = Actions.Update.PerformUpdate(cts.Token);
                break;
            case "check":
                if (args.Length != 1)
                {
                    Log.Error($"Wrong number of arguments passed");
                    return;
                }

                task = Actions.Check.PerformCheck(cts.Token);
                break;
            case "add":
                if (args.Length < 2)
                {
                    Log.Error($"Wrong number of arguments passed");
                    return;
                }

                task = Actions.Add.PerformAdd(args[1..], cts.Token);
                break;
            case "remove":
                if (args.Length < 2)
                {
                    Log.Error($"Wrong number of arguments passed");
                    return;
                }

                task = Actions.Remove.PerformRemove(args[1..], cts.Token);
                break;
            case "list":
                if (args.Length != 1)
                {
                    Log.Error($"Wrong number of arguments passed");
                    return;
                }

                task = Actions.List.PerformList(cts.Token);
                break;
            case "change":
                if (args.Length != 2)
                {
                    Log.Error($"Wrong number of arguments passed");
                    return;
                }

                task = Actions.Change.PerformChange(args[1], cts.Token);
                break;
            default:
                Log.Error($"Invalid action {args[0]}");
                return;
        }

        using (task)
        {
            while (!task.IsCompleted)
            {
                Thread.Yield();
            }

            if (task.Exception is not null)
            {
                Log.Error(task.Exception);
            }
        }
    }
}
