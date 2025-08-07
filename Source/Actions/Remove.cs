namespace MMM.Actions;

public static class Remove
{
    public static async Task PerformRemove(string modName, CancellationToken ct)
    {
        string? modId = await Context.Instance.GetModId(modName, ct);
        ModEntry? mod = Context.Instance.Modlist.Mods.FirstOrDefault(v => v.Id == modId);
        ModLock? modlock = Context.Instance.ModlistLock.FirstOrDefault(v => v.Id == modId);

        if (modlock is null)
        {
            Log.Error($"Mod {modName} not installed");
            return;
        }

        List<ModLock> usedBy = [.. Context.Instance.ModlistLock.Where(v => v.Dependencies.Contains(modlock.Id))];

        if (usedBy.Count > 0)
        {
            Log.Error($"Mod {modlock.Name ?? modName} is used by the following mod(s):");
            foreach (ModLock item in usedBy)
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

        Context.Instance.Modlist.Mods.Remove(mod);

        (List<ModDownloadInfo> updates, List<ModUninstallInfo> uninstalls) = await ModInstaller.CheckChanges(ct);

        await ModInstaller.PerformChanges(updates, uninstalls, ct);
    }
}