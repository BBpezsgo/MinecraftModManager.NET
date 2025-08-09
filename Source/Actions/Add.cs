namespace MMM.Actions;

public static class Add
{
    public static async Task PerformAdd(string[] queries, CancellationToken ct)
    {
        foreach (string query in queries)
        {
            if (Context.Instance.Modlist.Mods.Any(v => v.Id == query) &&
                Context.Instance.ModlistLock.Any(v => v.Id == query))
            {
                Log.Error($"Mod {query} already added");
                continue;
            }

            (string? id, string? name) = await ModrinthUtils.FindModOnline(query, ct);

            if (id is null) continue;

            if (Context.Instance.Modlist.Mods.Any(v => v.Id == id) &&
                Context.Instance.ModlistLock.Any(v => v.Id == id))
            {
                Log.Error($"Mod {name ?? query} already added");
                continue;
            }

            Context.Instance.Modlist.Mods.Add(new ModEntry()
            {
                Id = id,
                Name = name,
            });
        }

        Changes changes = await ModInstaller.CheckChanges(ct);

        await ModInstaller.PerformChanges(changes, ct);
    }
}