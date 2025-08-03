using Modrinth;

namespace MMM.Actions;

public static class Change
{
    public static async Task PerformChange(string targetVersion, CancellationToken ct)
    {
        ModrinthClient client = new(Settings.ModrinthClientConfig);
        Settings settings = Settings.Create();

        settings.Modlist.GameVersion = targetVersion;

        var (modUpdates, unsupported) = await Update.CheckNewVersions(settings, client, ct);

        await Install.PerformChanges(settings, modUpdates, unsupported, ct);
    }
}