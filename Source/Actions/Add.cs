using Logger;

namespace MMM.Actions;

public static class Add
{
    public static async Task PerformAdd(string[] queries, CancellationToken ct)
    {
        bool didAddSomething = false;

        foreach (string query in queries)
        {
            if (Context.Instance.Modlist.Mods.Any(v => v.Id == query) &&
                Context.Instance.ModlistLock.Any(v => v.Id == query))
            {
                Log.Error($"Mod {Context.Instance.GetModName(query)} already added");
                continue;
            }

            (string Id, string Name)? v = await ModrinthUtils.FindModOnline(query, ct);
            if (!v.HasValue) continue;
            string id = v.Value.Id;

            if (Context.Instance.Modlist.Mods.Any(v => v.Id == id))
            {
                Log.Error($"Mod {v.Value.Name} already added");
                continue;
            }

            if (Context.Instance.ModlistLock.Any(v => v.Id == id))
            {
                Log.Info($"Mod already installed");
            }

            Context.Instance.Modlist.Mods.Add(new ModEntry()
            {
                Id = id,
                Name = v.Value.Name,
            });
            didAddSomething = true;
        }

        if (!didAddSomething) return;

        Changes changes = await ModInstaller.CheckChanges(ct);

        await ModInstaller.PerformChanges(changes, ct);
    }
}