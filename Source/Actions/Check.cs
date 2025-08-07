using System.Collections.Immutable;

namespace MMM.Actions;

public static class Check
{
    static async Task PrintLockFileErrors(Context context, CancellationToken ct)
    {
        List<InstalledComponent> installedMods = [];

        ProgressBar progressBar = new();

        if (Directory.Exists(context.ModsDirectory))
        {
            string[] array = Directory.GetFiles(context.ModsDirectory, "*.jar");
            for (int i = 0; i < array.Length; i++)
            {
                string file = array[i];

                progressBar.Report(Path.GetFileName(file), i, array.Length);

                string hash = await Utils.ComputeHash1(file, ct);

                ModLock? lockEntry = context.ModlistLock.FirstOrDefault(v => v.FileName == Path.GetFileName(file));

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

        for (int i = 0; i < context.ModlistLock.Count; i++)
        {
            ModLock lockEntry = context.ModlistLock[i];

            progressBar.Report(lockEntry.Name ?? lockEntry.Id, i, context.ModlistLock.Count);

            string file = context.GetModPath(lockEntry);

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
        Log.Section($"Performing checks");

        Log.MajorAction($"Checking lockfile");

        await PrintLockFileErrors(Context.Instance, ct);

        Log.MajorAction($"Checking dependencies");
        (ImmutableArray<DependencyError> errors, _) = await DependencyResolution.CheckDependencies(Context.Instance, false, ct);

        foreach (DependencyError error in errors)
        {
            if (error.Level == DependencyErrorLevel.Depends)
            {
                (string? id, string? name) = await ModrinthUtils.FindModOnline(error.OtherId, ct);

                if (id is null) continue;

                Context.Instance.Modlist.Mods.Add(new ModEntry()
                {
                    Id = id,
                    Name = name,
                });
            }
        }

        (List<ModDownloadInfo> updates, List<ModUninstallInfo> uninstalls) = await ModInstaller.CheckChanges(ct);

        await ModInstaller.PerformChanges(updates, uninstalls, ct);
    }
}
