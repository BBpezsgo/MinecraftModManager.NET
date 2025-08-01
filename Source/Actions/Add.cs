using Modrinth;

namespace MMM.Actions;

public static class Add
{
    public static async Task PerformAdd(string mod, CancellationToken ct)
    {
        ModrinthClient client = new(Settings.ModrinthClientConfig);
        Settings settings = Settings.Create();

        if (settings.Modlist.Mods.Any(v => v.Id == mod) &&
            settings.ModlistLock.Any(v => v.Id == mod))
        {
            Log.Error($"Mod {mod} already added");
            return;
        }

        settings.Modlist.Mods.Add(new ModlistEntry()
        {
            Id = mod,
        });

        var (updates, uninstalls) = await Install.CheckChanges(settings, client, ct);

        await Install.PerformChanges(settings, updates, uninstalls, ct);
    }
}