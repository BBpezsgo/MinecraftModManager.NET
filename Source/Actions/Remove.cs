using Modrinth;

namespace MMM.Actions;

public static class Remove
{
    public static async Task PerformRemove(string mod, CancellationToken ct)
    {
        ModrinthClient client = new(Settings.ModrinthClientConfig);
        Settings settings = Settings.Create();

        int i = settings.Modlist.Mods.IndexOf(v => v.Id == mod);

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