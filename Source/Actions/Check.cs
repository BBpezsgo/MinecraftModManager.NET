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

        await Lockfile.GetLockfileErrors(settings.ModsDirectory, settings.ModlistLock, true, ct);

        Log.MajorAction($"Checking dependencies");
        var (errors, ok) = await DependencyResolution.CheckDependencies(settings, false, ct);

        ModrinthClient client = new(Settings.ModrinthClientConfig);

        foreach (var error in errors)
        {
            if (error.Level == DependencyErrorLevel.Depends)
            {
                Modrinth.Models.SearchResult? result = (await client.Project.SearchAsync(
                    error.OtherId.Replace('-', ' '),
                    Modrinth.Models.Enums.Index.Downloads,
                    0,
                    1,
                    null,
                    ct)).Hits.FirstOrDefault();

                if (result is null)
                {
                    Log.Error($"Mod {error.OtherId} not found online");
                    continue;
                }

                Log.Info($"Mod {error.OtherId} found online as {result.Title} by {result.Author}");

                if (!Log.AskYesNo($"Is this the correct mod?"))
                {
                    continue;
                }

                settings.Modlist.Mods.Add(new ModlistEntry()
                {
                    Id = result.ProjectId,
                    Name = result.Title,
                });
            }
        }

        var (updates, uninstalls) = await Install.CheckChanges(settings, client, ct);

        await Install.PerformChanges(settings, updates, uninstalls, ct);
    }
}
