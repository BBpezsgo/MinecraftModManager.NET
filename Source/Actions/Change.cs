namespace MMM.Actions;

public static class Change
{
    public static async Task PerformChange(string targetVersion, CancellationToken ct)
    {
        Context.Instance.Modlist.GameVersion = targetVersion;

        (List<ModDownloadInfo> modUpdates, List<ModUninstallInfo> unsupported) = await Update.CheckNewVersions(ct);

        await ModInstaller.PerformChanges(modUpdates, unsupported, ct);
    }
}