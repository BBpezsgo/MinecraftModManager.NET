using System.Collections.Immutable;
using Modrinth;

namespace MMM.Actions;

public static class Check
{
    public static async Task PerformCheck(CancellationToken ct)
    {
        Settings settings = Settings.Create();

        Log.Section($"Performing checks");

        Log.MajorAction($"Checking lockfile");

        await LockFile.GetLockfileErrors(settings.ModsDirectory, settings.ModlistLock, true, ct);

        Log.MajorAction($"Checking dependencies");
        var (errors, ok) = await DependencyResolution.CheckDependencies(settings, false, ct);

        ModrinthClient client = new(Settings.ModrinthClientConfig);

        foreach (var error in errors)
        {
            if (error.Level == DependencyErrorLevel.Depends)
            {
                (string? id, string? name) = await Install.FindModOnline(error.OtherId, client, ct);

                if (id is null) continue;

                settings.Modlist.Mods.Add(new ModEntry()
                {
                    Id = id,
                    Name = name,
                });
            }
        }

        var (updates, uninstalls) = await Install.CheckChanges(settings, client, ct);

        await Install.PerformChanges(settings, updates, uninstalls, ct);
    }
}
