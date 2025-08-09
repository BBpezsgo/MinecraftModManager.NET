namespace MMM.Actions;

public static class List
{
    public static Task PerformList(CancellationToken ct) => Task.Run(() =>
    {
        Log.MajorAction("Reading mods");

        foreach (ModLock lockEntry in Context.Instance.ModlistLock)
        {
            ModEntry? modEntry = Context.Instance.Modlist.Mods.FirstOrDefault(v => v.Id == lockEntry.Id);
            bool isDependency = Context.Instance.ModlistLock.Any(v => v.Dependencies.Contains(lockEntry.Id));

            string? name = Context.Instance.GetModName(lockEntry.Id) ?? lockEntry.FileName;

            Log.Write($"{name} ({lockEntry.Id})");

            if (modEntry is null)
            {
                if (isDependency)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Log.Write(" (dependency)");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Log.Write(" (orphan)");
                    Console.ResetColor();
                }
            }

            Log.NewLine();
        }
    }, ct);
}
