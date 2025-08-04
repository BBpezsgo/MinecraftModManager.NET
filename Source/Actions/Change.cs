using Modrinth;

namespace MMM.Actions;

public static class Change
{
    public static async Task PerformChange(string targetVersion, CancellationToken ct)
    {
        ModrinthClient client = new(Context.ModrinthClientConfig);
        Context settings = Context.Create();

        settings.Modlist.GameVersion = targetVersion;

        (List<ModDownloadInfo> modUpdates, List<ModUninstallInfo> unsupported) = await Update.CheckNewVersions(settings, client, ct);

        await ModInstaller.PerformChanges(settings, modUpdates, unsupported, ct);
    }
}