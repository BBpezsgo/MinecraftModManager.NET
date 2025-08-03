using System.Collections.Immutable;
using Modrinth;

namespace MMM.Actions;

public static class Update
{
    public static async Task PerformUpdate(CancellationToken ct)
    {
        ModrinthClient client = new(Settings.ModrinthClientConfig);
        Settings settings = Settings.Create();

        var (modUpdates, unsupported) = await CheckNewVersions(settings, client, ct);

        await Install.PerformChanges(settings, modUpdates, unsupported, ct);
    }

    public static async Task<(List<ModUpdate> Updates, List<ModUninstall> Unsupported)> CheckNewVersions(Settings settings, ModrinthClient client, CancellationToken ct)
    {
        List<ModUpdate> modUpdates = [];

        Log.Section($"Checking for updates");
        ProgressBar progressBar = new();

        HashSet<string> checkTheseSet = [];

        void AddThese(IEnumerable<string> mods)
        {
            foreach (string mod in mods)
            {
                if (!checkTheseSet.Add(mod)) continue;
                List<string> dependencies = settings.ModlistLock.FirstOrDefault(v => v.Id == mod)?.Dependencies ?? [];
                AddThese(dependencies);
            }
        }

        AddThese(settings.Modlist.Mods.Select(v => v.Id));

        ImmutableArray<string> checkThese = [.. checkTheseSet];
        List<ModUninstall> unsupportedMods = [];

        for (int i = 0; i < checkThese.Length; i++)
        {
            string modId = checkThese[i];

            progressBar.Report(settings.GetModName(modId) ?? modId, i, checkThese.Length);

            ModUpdate? update;
            try
            {
                update = await Install.GetModIfNeeded(modId, settings, client, ct);
            }
            catch (ModNotSupported e)
            {
                var modLock = settings.ModlistLock.FirstOrDefault(v => v.Id == modId);
                if (modLock is not null)
                {
                    string file = Path.GetFullPath(Path.Combine(settings.ModsDirectory, modLock.FileName));
                    unsupportedMods.Add(new ModUninstall()
                    {
                        Reason = ModUninstallReason.Because,
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

public enum ModUpdateReason
{
    NewVersion,
    HashChanged,
    NotInstalled,
    InvalidHash,
}

public class ModUpdate : ModDownloadInfo
{
    public required ModUpdateReason Reason { get; init; }
}

public class ModUninstall : ModUninstallInfo
{
    public required ModUninstallReason Reason { get; init; }
}

