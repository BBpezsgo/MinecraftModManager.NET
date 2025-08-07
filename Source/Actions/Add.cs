namespace MMM.Actions;

public static class Add
{
    public static async Task PerformAdd(string query, CancellationToken ct)
    {
        if (Context.Instance.Modlist.Mods.Any(v => v.Id == query) &&
            Context.Instance.ModlistLock.Any(v => v.Id == query))
        {
            Log.Error($"Mod {query} already added");
            return;
        }

        (string? id, string? name) = await ModrinthUtils.FindModOnline(query, ct);

        if (id is null) return;

        if (Context.Instance.Modlist.Mods.Any(v => v.Id == id) &&
            Context.Instance.ModlistLock.Any(v => v.Id == id))
        {
            Log.Error($"Mod {name ?? query} already added");
            return;
        }

        Context.Instance.Modlist.Mods.Add(new ModEntry()
        {
            Id = id,
            Name = name,
        });

        (List<ModDownloadInfo> updates, List<ModUninstallInfo> uninstalls) = await ModInstaller.CheckChanges(ct);

        await ModInstaller.PerformChanges(updates, uninstalls, ct);
    }
}