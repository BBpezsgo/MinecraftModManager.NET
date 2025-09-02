using System.Buffers;
using Modrinth.Exceptions;

namespace MMM;

static class ModrinthUtils
{
    public static async Task<(string Id, string Name)?> FindModOnline(string query, CancellationToken ct)
    {
        if (IsModrinthId(query))
        {
            try
            {
                Log.Debug($"Fetching metadata for mod {query}");
                Modrinth.Models.Project project = await Context.Modrinth.Project.GetAsync(query, ct);
                return (project.Id, project.Title);
            }
            catch (ModrinthApiException)
            {

            }
        }

        Log.Debug($"Searching online for mod {query}");
        Modrinth.Models.SearchResult? result = (await Context.Modrinth.Project.SearchAsync(
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

        string name = result.Title ?? await GetModName(result.ProjectId, ct);

        return (result.ProjectId, name);
    }

    static readonly SearchValues<char> ModrinthIdSearch = SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

    public static bool IsModrinthId(string value) => value.Length == 8 && value.All(ModrinthIdSearch.Contains);

    public static async Task<ModInstallInfo> GetModDownload(string id, CancellationToken ct)
    {
        Modrinth.Models.Version[] versions;
        try
        {
            Log.Debug($"Fetching versions for mod {id}");
            versions = await Context.Modrinth.Version.GetProjectVersionListAsync(
                id,
                [Context.Instance.Modlist.Loader],
                [Context.Instance.Modlist.GameVersion],
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

        string name = await GetModName(id, ct);

        foreach (Modrinth.Models.Version version in versions)
        {
            if (!version.GameVersions.Contains(Context.Instance.Modlist.GameVersion)) continue;
            if (!version.Loaders.Contains(Context.Instance.Modlist.Loader)) continue;

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

            ModLock? lockfileEntry = Context.Instance.ModlistLock.FirstOrDefault(v => v.Id == id);

            return new ModInstallInfo()
            {
                Reason = ModUpdateReason.Because,
                Mod = Context.Instance.Modlist.Mods.FirstOrDefault(v => v.Id == id) ?? new ModEntry()
                {
                    Id = id,
                    Name = name,
                },
                File = file,
                Version = version,
                LockEntry = lockfileEntry,
                DownloadFileName = downloadFileName,
                Name = name,
            };
        }

        throw new ModNotSupported($"Mod {name} not supported");
    }

    public static async Task<ModInstallInfo?> GetModIfNeeded(string id, CancellationToken ct)
    {
        ModInstallInfo downloadInfo = await GetModDownload(id, ct);

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
            ? new ModInstallInfo()
            {
                Reason = reason,
                Mod = Context.Instance.Modlist.Mods.FirstOrDefault(v => v.Id == id) ?? new ModEntry()
                {
                    Id = id,
                    Name = downloadInfo.Name,
                },
                DownloadFileName = downloadInfo.DownloadFileName,
                File = downloadInfo.File,
                LockEntry = downloadInfo.LockEntry,
                Version = downloadInfo.Version,
                Name = downloadInfo.Name,
            }
            : null;
    }

    static readonly Dictionary<string, string> _idNameCache = [];

    public static async Task<string> GetModNameForce(string id, CancellationToken ct)
    {
        if (_idNameCache.TryGetValue(id, out string? name)) return name;

        Log.Debug($"Fetching metadata for mod {id}");
        Modrinth.Models.Project project = await Context.Modrinth.Project.GetAsync(id, ct);
        return _idNameCache[id] = project.Title;
    }

    public static async Task<string> GetModName(string id, CancellationToken ct)
        => Context.Instance.GetModName(id) ?? await GetModNameForce(id, ct);
}