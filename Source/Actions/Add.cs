using Modrinth;

namespace MMM.Actions;

public static class Add
{
    public static async Task PerformAdd(string query, CancellationToken ct)
    {
        ModrinthClient client = new(Context.ModrinthClientConfig);
        Context settings = Context.Create();

        if (settings.Modlist.Mods.Any(v => v.Id == query) &&
            settings.ModlistLock.Any(v => v.Id == query))
        {
            Log.Error($"Mod {query} already added");
            return;
        }

        (string? id, string? name) = await ModrinthUtils.FindModOnline(query, client, ct);

        if (id is null) return;

        if (settings.Modlist.Mods.Any(v => v.Id == id) &&
            settings.ModlistLock.Any(v => v.Id == id))
        {
            Log.Error($"Mod {name ?? query} already added");
            return;
        }

        settings.Modlist.Mods.Add(new ModEntry()
        {
            Id = id,
            Name = name,
        });

        (List<ModDownloadInfo> updates, List<ModUninstallInfo> uninstalls) = await ModInstaller.CheckChanges(settings, client, ct);

        await ModInstaller.PerformChanges(settings, updates, uninstalls, ct);
    }
}