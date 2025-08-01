using Modrinth;

namespace MMM.Actions;

public static class Remove
{
    public static async Task PerformRemove(string mod, CancellationToken ct)
    {
        ModrinthClient client = new(Settings.ModrinthClientConfig);
        Settings settings = Settings.Create();

        int i = -1;
        for (int j = 0; j < settings.Modlist.Mods.Count; j++)
        {
            ModlistEntry v = settings.Modlist.Mods[j];

            if (v.Id == mod) goto found;
            if (v.Name is not null && v.Name.Equals(mod, StringComparison.InvariantCultureIgnoreCase)) goto found;

            var lockfile = settings.ModlistLock.FirstOrDefault(l => v.Id == l.Id);
            if (lockfile is null) continue;

            string filename = Path.GetFullPath(Path.Combine(settings.ModsDirectory, lockfile.FileName));
            if (!File.Exists(filename)) continue;

            Fabric.FabricMod? fabricMod;
            try
            {
                fabricMod = await InstalledMods.ReadMod(filename, ct);
            }
            catch (Exception e)
            {
                Log.Error(e);
                continue;
            }

            if (fabricMod.Id.Equals(mod, StringComparison.InvariantCultureIgnoreCase)) goto found;
            if (fabricMod.Name is not null && fabricMod.Name.Equals(mod, StringComparison.InvariantCultureIgnoreCase)) goto found;

            continue;

        found:;
            if (i != -1)
            {
                Log.Error($"Multiple mods found");
                return;
            }

            i = j;
        }

        if (i == -1)
        {
            Log.Error($"Mod {mod} not installed");
            return;
        }

        settings.Modlist.Mods.RemoveAt(i);

        var (updates, uninstalls) = await Install.CheckChanges(settings, client, ct);

        await Install.PerformChanges(settings, updates, uninstalls, ct);
    }
}