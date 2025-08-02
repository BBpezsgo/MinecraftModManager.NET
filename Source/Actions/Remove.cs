using System.Runtime.Serialization;
using Modrinth;

namespace MMM.Actions;

public static class Remove
{
    public static async Task PerformRemove(string modName, CancellationToken ct)
    {
        ModrinthClient client = new(Settings.ModrinthClientConfig);
        Settings settings = Settings.Create();

        string? modId = await settings.GetModId(modName, ct);
        ModlistEntry? mod = settings.Modlist.Mods.FirstOrDefault(v => v.Id == modId);
        ModlistLockEntry? modlock = settings.ModlistLock.FirstOrDefault(v => v.Id == modId);

        if (modlock is null)
        {
            Log.Error($"Mod {modName} not installed");
            return;
        }

        List<ModlistLockEntry> usedBy = [.. settings.ModlistLock.Where(v => v.Dependencies.Contains(modlock.Id))];

        if (usedBy.Count > 0)
        {
            Log.Error($"Mod {modlock.Name ?? modName} is used by the following mod(s):");
            foreach (var item in usedBy)
            {
                Log.None(item.Name ?? item.FileName ?? item.Id);
            }
        }

        if (mod is null)
        {
            Log.Error($"Mod {modName} was implicitly installed therefore cannot be uninstalled");
            return;
        }

        if (usedBy.Count > 0 && !Log.AskYesNo("Do you want to continue?", false))
        {
            return;
        }

        settings.Modlist.Mods.Remove(mod);

        var (updates, uninstalls) = await Install.CheckChanges(settings, client, ct);

        await Install.PerformChanges(settings, updates, uninstalls, ct);
    }
}