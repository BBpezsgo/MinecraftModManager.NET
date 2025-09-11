using Logger;

namespace MMM.Actions;

public static class Remove
{
    public static async Task PerformRemove(string[] modNames, CancellationToken ct)
    {
        List<(string ModId, ModEntry? Mod, ModLock Lock)> toUninstall = [];

        foreach (string modName in modNames)
        {
            string? modId = await Context.Instance.GetModId(modName, ct);

            if (modId is null)
            {
                Log.Error($"Mod {modName} not found");
                continue;
            }

            ModEntry? mod = Context.Instance.Modlist.Mods.FirstOrDefault(v => v.Id == modId);
            ModLock? modlock = Context.Instance.ModlistLock.FirstOrDefault(v => v.Id == modId);

            if (modlock is null)
            {
                Log.Error($"Mod {modName} not installed");
                continue;
            }

            toUninstall.Add((modId, mod, modlock));
        }

        foreach ((string modId, ModEntry? mod, ModLock modlock) in toUninstall)
        {
            string modName = await ModrinthUtils.GetModName(modId, ct);

            List<ModLock> usedBy = [
                .. Context.Instance.ModlistLock
                    .Where(v => toUninstall.All(w => v.Id != w.ModId))
                    .Where(v => v.Dependencies.Contains(modlock.Id))
            ];

            if (usedBy.Count > 0)
            {
                Log.Error($"Mod {modName} is used by the following mod(s):");
                foreach (ModLock item in usedBy)
                {
                    Log.None(item.Name ?? item.FileName ?? item.Id);
                }

                if (mod is null) continue;
            }

            if (mod is null)
            {
                Log.Error($"Mod {modName} was implicitly installed therefore cannot be uninstalled");
                continue;
            }

            if (usedBy.Count > 0 && !Log.AskYesNo("Do you want to continue?", false))
            {
                continue;
            }

            Context.Instance.Modlist.Mods.Remove(mod);
        }

        Changes changes = await ModInstaller.CheckChanges(ct);

        await ModInstaller.PerformChanges(changes, ct);
    }
}