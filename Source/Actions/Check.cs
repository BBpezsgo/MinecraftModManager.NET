using System.Collections.Immutable;
using Modrinth;

namespace MMM.Actions;

public static class Check
{
    static async Task PrintLockFileErrors(string modsDirectory, List<ModLock> modlistLock, CancellationToken ct)
    {
        List<InstalledComponent> installedMods = [];

        ProgressBar progressBar = new();

        if (Directory.Exists(modsDirectory))
        {
            string[] array = Directory.GetFiles(modsDirectory, "*.jar");
            for (int i = 0; i < array.Length; i++)
            {
                string file = array[i];

                progressBar.Report(Path.GetFileName(file), i, array.Length);

                string hash = await Utils.ComputeHash1(file, ct);

                ModLock? lockEntry = modlistLock.FirstOrDefault(v => v.FileName == Path.GetFileName(file));

                if (lockEntry is null)
                {
                    Log.Error($"File {Path.GetFileName(file)} does not exists in the lockfile");
                    continue;
                }

                if (hash != lockEntry.Hash)
                {
                    Log.Error($"File {Path.GetFileName(file)} checksum mismatched");
                    continue;
                }
            }
        }

        for (int i = 0; i < modlistLock.Count; i++)
        {
            ModLock lockEntry = modlistLock[i];

            progressBar.Report(lockEntry.Name ?? lockEntry.Id, i, modlistLock.Count);

            string file = Path.GetFullPath(Path.Combine(modsDirectory, lockEntry.FileName));

            if (!File.Exists(file))
            {
                Log.Error($"File {file} does not exists");
                continue;
            }
        }

        progressBar.Dispose();
    }

    public static async Task PerformCheck(CancellationToken ct)
    {
        Context settings = Context.Create();

        Log.Section($"Performing checks");

        Log.MajorAction($"Checking lockfile");

        await PrintLockFileErrors(settings.ModsDirectory, settings.ModlistLock, ct);

        Log.MajorAction($"Checking dependencies");
        (ImmutableArray<DependencyError> errors, _) = await DependencyResolution.CheckDependencies(settings, false, ct);

        ModrinthClient client = new(Context.ModrinthClientConfig);

        foreach (DependencyError error in errors)
        {
            if (error.Level == DependencyErrorLevel.Depends)
            {
                (string? id, string? name) = await ModrinthUtils.FindModOnline(error.OtherId, client, ct);

                if (id is null) continue;

                settings.Modlist.Mods.Add(new ModEntry()
                {
                    Id = id,
                    Name = name,
                });
            }
        }

        (List<ModDownloadInfo> updates, List<ModUninstallInfo> uninstalls) = await ModInstaller.CheckChanges(settings, client, ct);

        await ModInstaller.PerformChanges(settings, updates, uninstalls, ct);
    }
}
