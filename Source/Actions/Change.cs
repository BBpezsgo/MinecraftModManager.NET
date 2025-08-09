namespace MMM.Actions;

public static class Change
{
    public static async Task PerformChange(string targetVersion, CancellationToken ct)
    {
        Context.Instance.Modlist.GameVersion = targetVersion;

        Changes changes = await Update.CheckNewVersions(ct);

        await ModInstaller.PerformChanges(changes, ct);
    }
}