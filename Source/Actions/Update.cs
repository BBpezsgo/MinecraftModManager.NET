using System.Collections.Immutable;
using Modrinth;

namespace MMM.Actions;

public static class Update
{
    public static async Task PerformUpdate(CancellationToken ct)
    {
        ModrinthClient client = new(Settings.ModrinthClientConfig);
        Settings settings = Settings.Create();

        List<ModUpdate> modUpdates = [];

        Log.Section($"Checking for updates");
        ProgressBar progressBar = new();

        HashSet<string> checkTheseSet = [];

        void AddThese(IEnumerable<string> mods)
        {
            foreach (string mod in mods)
            {
                if (!checkTheseSet.Add(mod)) continue;
                List<string> dependencies = settings.ModlistLock.FirstOrDefault(v => v.Id == mod)?.Dependencies ?? [];
                AddThese(dependencies);
            }
        }

        AddThese(settings.Modlist.Mods.Select(v => v.Id));

        ImmutableArray<string> checkThese = [.. checkTheseSet];

        for (int i = 0; i < checkThese.Length; i++)
        {
            string modId = checkThese[i];
            string modName = settings.GetModName(modId) ?? modId;

            progressBar.Report(modName, i, checkThese.Length);

            ModUpdate? update;
            try
            {
                update = await Install.FindModIfNeeded(modId, settings, client, ct);
            }
            catch (Exception e)
            {
                Log.Error(e);
                continue;
            }

            if (update is null) continue;

            modUpdates.Add(update);
        }

        progressBar.Dispose();

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

