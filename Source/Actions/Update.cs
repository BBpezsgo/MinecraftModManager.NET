using System.Collections.Immutable;

namespace MMM.Actions;

public static class Update
{
    public static async Task PerformUpdate(CancellationToken ct)
    {
        Changes changes = await CheckNewVersions(ct);

        await ModInstaller.PerformChanges(changes, ct);
    }

    public static async Task<Changes> CheckNewVersions(CancellationToken ct)
    {
        ImmutableArray<ModInstallInfo>.Builder modUpdates = ImmutableArray.CreateBuilder<ModInstallInfo>();
        ImmutableArray<ModUninstallInfo>.Builder unsupportedMods = ImmutableArray.CreateBuilder<ModUninstallInfo>();

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

        List<string> unsupportedModIds = [];

        for (int i = 0; i < checkThese.Length; i++)
        {
            string modId = checkThese[i];

            progressBar.Report(Context.Instance.GetModName(modId) ?? modId, i, checkThese.Length);

            ModInstallInfo? update;
            try
            {
                update = await ModrinthUtils.GetModIfNeeded(modId, ct);
            }
            catch (ModNotSupported e)
            {
                unsupportedModIds.Add(modId);
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

        if (unsupportedModIds.Count > 0 && Log.AskYesNo($"Do you want to uninstall {unsupportedModIds.Count} unsupported mods?", true))
        {
            foreach (string modId in unsupportedModIds)
            {
                string name = await ModrinthUtils.GetModName(modId, ct);

                foreach (ModLock other in Context.Instance.ModlistLock)
                {
                    if (other.Dependencies.Contains(modId))
                    {
                        Log.Error($"Mod {name} is unsupported but required by mod {await ModrinthUtils.GetModName(other.Id, ct)}");
                    }
                }

                ModEntry? mod = Context.Instance.Modlist.Mods.FirstOrDefault(v => v.Id == modId);
                if (mod is not null) Context.Instance.Modlist.Mods.Remove(mod);

                ModLock? modLock = Context.Instance.ModlistLock.FirstOrDefault(v => v.Id == modId);
                if (modLock is not null)
                {
                    string file = Context.Instance.GetModPath(modLock);
                    unsupportedMods.Add(new ModUninstallInfo()
                    {
                        Reason = ModUninstallReason.NotSupported,
                        LockEntry = modLock,
                        File = File.Exists(file) ? file : null,
                        Name = name,
                    });
                }
                else
                {
                    unsupportedMods.Add(new ModUninstallInfo()
                    {
                        Reason = ModUninstallReason.NotSupported,
                        LockEntry = null,
                        File = null,
                        Name = name,
                    });
                }
            }
        }

        if (modUpdates.Count == 0)
        {
            Log.Info($"All mods are up to date");
        }

        return new Changes()
        {
            Install = modUpdates.ToImmutable(),
            Uninstall = unsupportedMods.ToImmutable(),
        };
    }
}
