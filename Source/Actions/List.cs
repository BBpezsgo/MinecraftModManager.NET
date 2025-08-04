namespace MMM.Actions;

public static class List
{
    public static async Task PerformList(CancellationToken ct)
    {
        Context settings = Context.Create();

        Log.MajorAction("Reading mods");

        foreach (ModLock lockEntry in settings.ModlistLock)
        {
            ModEntry? modEntry = settings.Modlist.Mods.FirstOrDefault(v => v.Id == lockEntry.Id);
            bool isDependency = settings.ModlistLock.Any(v => v.Dependencies.Contains(lockEntry.Id));

            string? name = settings.GetModName(lockEntry.Id) ?? lockEntry.FileName;

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
    }
}
