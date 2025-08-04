using System.Buffers;
using Modrinth;
using Modrinth.Exceptions;

namespace MMM;

static class ModrinthUtils
{
    public static async Task<(string? Id, string? Name)> FindModOnline(string query, ModrinthClient client, CancellationToken ct)
    {
        if (IsModrinthId(query))
        {
            try
            {
                Modrinth.Models.Project project = await client.Project.GetAsync(query, ct);
                return (project.Id, project.Title);
            }
            catch (ModrinthApiException)
            {

            }
        }

        Modrinth.Models.SearchResult? result = (await client.Project.SearchAsync(
            query.Replace('-', ' '),
            Modrinth.Models.Enums.Index.Downloads,
            0,
            1,
            null,
            ct)).Hits.FirstOrDefault();

        if (result is null)
        {
            Log.Error($"Mod {query} not found online");
            return default;
        }

        if (string.Equals(result.Title, query, StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning($"Mod {query} found online as {result.Title} by {result.Author}");
        }
        else
        {
            Log.Info($"Mod {query} found online as {result.Title} by {result.Author}");

            if (!Log.AskYesNo($"Is this the mod above correct?", true))
            {
                return default;
            }
        }

        return (result.ProjectId, result.Title);
    }

    static readonly SearchValues<char> ModrinthIdSearch = SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

    public static bool IsModrinthId(string value) => value.Length == 8 && value.All(ModrinthIdSearch.Contains);

    public static async Task<ModDownloadInfo> GetModDownload(string id, Context settings, ModrinthClient client, CancellationToken ct)
    {
        Modrinth.Models.Version[] versions;
        try
        {
            versions = await client.Version.GetProjectVersionListAsync(
                id,
                [settings.Modlist.Loader],
                [settings.Modlist.GameVersion],
                null,
                ct
            );
        }
        catch (ModrinthApiException e)
        {
            if (e.Response?.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new ModNotFoundException($"Mod {id} not found", e);
            }
            throw;
        }

        Array.Sort(versions, (a, b) =>
        {
            if (a.DatePublished < b.DatePublished) return 1;
            if (a.DatePublished > b.DatePublished) return -1;
            return 0;
        });

        foreach (Modrinth.Models.Version version in versions)
        {
            if (!version.GameVersions.Contains(settings.Modlist.GameVersion)) continue;
            if (!version.Loaders.Contains(settings.Modlist.Loader)) continue;

            Modrinth.Models.File? file =
                version.Files.FirstOrDefault(v => v.Primary)
                ?? version.Files.FirstOrDefault();

            if (file is null)
            {
                Log.Warning($"Version {version.Name} does not have a file");
                continue;
            }

            string downloadFileName = file.FileName ?? Path.GetFileName(new Uri(file.Url).LocalPath);

            if (downloadFileName.Contains('\\') || downloadFileName.Contains('/'))
            {
                Log.Warning($"Invalid filename \"{downloadFileName}\" for version {version.Name}");
                continue;
            }

            ModLock? lockfileEntry = settings.ModlistLock.FirstOrDefault(v => v.Id == id);

            return new ModDownloadInfo()
            {
                Reason = ModUpdateReason.Because,
                Mod = settings.Modlist.Mods.FirstOrDefault(v => v.Id == id) ?? new ModEntry() { Id = id },
                File = file,
                Version = version,
                LockEntry = lockfileEntry,
                DownloadFileName = downloadFileName,
            };
        }

        throw new ModNotSupported($"Mod {settings.GetModName(id) ?? id} not supported");
    }

    public static async Task<ModDownloadInfo?> GetModIfNeeded(string id, Context settings, ModrinthClient client, CancellationToken ct)
    {
        ModDownloadInfo downloadInfo = await GetModDownload(id, settings, client, ct);

        bool needsDownload = false;
        ModUpdateReason reason = default;

        if (downloadInfo.LockEntry is null)
        {
            needsDownload = true;
            reason = ModUpdateReason.NotInstalled;
        }
        else if (downloadInfo.LockEntry.ReleasedOn != downloadInfo.Version.DatePublished.ToString("o"))
        {
            needsDownload = true;
            reason = ModUpdateReason.NewVersion;
        }
        else if (downloadInfo.LockEntry.Hash != downloadInfo.File.Hashes.Sha1)
        {
            needsDownload = true;
            reason = ModUpdateReason.HashChanged;
        }

        return needsDownload
            ? new ModDownloadInfo()
            {
                Reason = reason,
                Mod = settings.Modlist.Mods.FirstOrDefault(v => v.Id == id) ?? new ModEntry() { Id = id },
                DownloadFileName = downloadInfo.DownloadFileName,
                File = downloadInfo.File,
                LockEntry = downloadInfo.LockEntry,
                Version = downloadInfo.Version,
            }
            : null;
    }
}