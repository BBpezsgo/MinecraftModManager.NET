using Modrinth;

namespace MMM.Actions;

public static class Add
{
    public static async Task PerformAdd(string query, CancellationToken ct)
    {
        ModrinthClient client = new(Settings.ModrinthClientConfig);
        Settings settings = Settings.Create();

        if (settings.Modlist.Mods.Any(v => v.Id == query) &&
            settings.ModlistLock.Any(v => v.Id == query))
        {
            Log.Error($"Mod {query} already added");
            return;
        }

        (string? id, string? name) = await Install.FindModOnline(query, client, ct);

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
            Name = query,
        });

        var (updates, uninstalls) = await Install.CheckChanges(settings, client, ct);

        await Install.PerformChanges(settings, updates, uninstalls, ct);
    }
}