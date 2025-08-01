using System.Collections.Immutable;
using Modrinth;

namespace MMM.Actions;

public static class Check
{
    public static async Task PerformCheck(CancellationToken ct)
    {
        Settings settings = Settings.Create();

        Log.MajorAction($"Checking lockfile");
        ImmutableArray<LockfileError> lockfileErrors = await Lockfile.GetLockfileErrors(settings.ModsDirectory, settings.ModlistLock, ct);
        foreach (LockfileError error in lockfileErrors)
        {
            switch (error.Status)
            {
                case LockfileStatus.NotTracked:
                    Log.Error($"File {Path.GetFileName(error.File)} does not exists in the lockfile");
                    break;
                case LockfileStatus.ChecksumMismatch:
                    Log.Error($"File {Path.GetFileName(error.File)} checksum mismatched");
                    break;
                case LockfileStatus.NotExists:
                    Log.Error($"File {error.File} does not exists");
                    break;
            }
        }

        Log.MajorAction($"Checking dependencies");
        var (errors, ok) = await DependencyResolution.CheckDependencies(settings, true, ct);

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

                Log.Notice($"Mod {error.OtherId} found online as {result.Title} by {result.Author}");

                if (!Log.AskYesNo($"Is this the correct mod? [y/n]"))
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
