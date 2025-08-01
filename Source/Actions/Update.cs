using Modrinth;

namespace MMM.Actions;

public static class Update
{
    public static async Task PerformUpdate(CancellationToken ct)
    {
        ModrinthClient client = new(Settings.ModrinthClientConfig);
        Settings settings = Settings.Create();

        List<ModUpdate> modUpdates = [];

        Log.MajorAction($"Checking for updates ...");
        LogEntry lastLine = default;
        Log.Keep(lastLine);

        for (int i = 0; i < settings.Modlist.Mods.Count; i++)
        {
            var mod = settings.Modlist.Mods[i];

            lastLine.Back();
            Log.Rekeep(lastLine =
                Log.Write(mod.Name ?? mod.Id) +
                Log.Write(new string(' ', Math.Max(0, Console.WindowWidth / 2 - (mod.Name ?? mod.Id).Length))) +
                Log.Progress(i, settings.Modlist.Mods.Count));

            ModUpdate? update;
            try
            {
                update = await Install.FindModIfNeeded(mod.Id, settings, client, ct);
            }
            catch (Exception e)
            {
                Log.Error(e);
                continue;
            }

            if (update is null) continue;

            modUpdates.Add(update);
        }

        lastLine.Clear();
        Log.Unkeep();

        await Install.PerformChanges(settings, modUpdates, [], ct);
    }
}

public enum ModUpdateReason
{
    NewVersion,
    HashChanged,
    NotInstalled,
    InvalidHash,
}

public class ModUpdate : ModDownloadInfo
{
    public required ModUpdateReason Reason { get; init; }
}

public class ModUninstall : ModUninstallInfo
{
    public required ModUninstallReason Reason { get; init; }
}

