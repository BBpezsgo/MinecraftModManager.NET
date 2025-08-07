using System.Collections.Immutable;

namespace MMM.Actions;

public static class Update
{
    public static async Task PerformUpdate(CancellationToken ct)
    {
        (List<ModDownloadInfo> modUpdates, List<ModUninstallInfo> unsupported) = await CheckNewVersions(ct);

        await ModInstaller.PerformChanges(modUpdates, unsupported, ct);
    }

    public static async Task<(List<ModDownloadInfo> Updates, List<ModUninstallInfo> Unsupported)> CheckNewVersions(CancellationToken ct)
    {
        List<ModDownloadInfo> modUpdates = [];

        Log.Section($"Checking for updates");
        ProgressBar progressBar = new();

        HashSet<string> checkTheseSet = [];

        void AddThese(IEnumerable<string> mods)
        {
            foreach (string mod in mods)
            {
                if (!checkTheseSet.Add(mod)) continue;
                List<string> dependencies = Context.Instance.ModlistLock.FirstOrDefault(v => v.Id == mod)?.Dependencies ?? [];
                AddThese(dependencies);
            }
        }

        AddThese(Context.Instance.Modlist.Mods.Select(v => v.Id));

        ImmutableArray<string> checkThese = [.. checkTheseSet];
        List<ModUninstallInfo> unsupportedMods = [];

        for (int i = 0; i < checkThese.Length; i++)
        {
            string modId = checkThese[i];

            progressBar.Report(Context.Instance.GetModName(modId) ?? modId, i, checkThese.Length);

            ModDownloadInfo? update;
            try
            {
                update = await ModrinthUtils.GetModIfNeeded(modId, ct);
            }
            catch (ModNotSupported e)
            {
                ModLock? modLock = Context.Instance.ModlistLock.FirstOrDefault(v => v.Id == modId);
                if (modLock is not null)
                {
                    string file = Context.Instance.GetModPath(modLock);
                    unsupportedMods.Add(new ModUninstallInfo()
                    {
                        Reason = ModUninstallReason.NotSupported,
                        LockEntry = modLock,
                        File = File.Exists(file) ? file : null,
                    });
                }
                Log.Error(e);
                continue;
            }
            catch (Exception e)
            {
                Log.Error(e);
                continue;
            }

            if (update is null) continue;

            modUpdates.Add(update);
        }

        progressBar.Dispose();

        return (modUpdates, unsupportedMods);
    }
}
