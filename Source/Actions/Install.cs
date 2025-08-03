using System.Buffers;
using System.Collections.Immutable;
using System.Text.Json;
using Modrinth;
using Modrinth.Exceptions;

namespace MMM.Actions;

public class ModDownloadInfo
{
    public required Modrinth.Models.Version Version { get; init; }
    public required Modrinth.Models.File File { get; init; }
    public required ModLock? LockEntry { get; init; }
    public required string DownloadFileName { get; init; }
    public required ModEntry Mod { get; init; }
}

public enum ModUninstallReason
{
    Because,
}

public class ModUninstallInfo
{
    public required string? File { get; init; }
    public required ModLock? LockEntry { get; init; }
}

public static class Install
{
    public static async Task DownloadMod(ModDownloadInfo info, Settings settings, CancellationToken ct)
    {
        if (!Directory.Exists(settings.ModsDirectory))
        {
            Directory.CreateDirectory(settings.ModsDirectory);
        }

        string filepath = Path.GetFullPath(Path.Combine(settings.ModsDirectory, info.DownloadFileName));
        //if (!File.Exists(filepath)) File.Create(filepath).Dispose();
        using var httpClient = new HttpClient();
        using var s = await httpClient.GetStreamAsync(info.File.Url, ct);
        using var fs = new FileStream(filepath, FileMode.Create);
        await s.CopyToAsync(fs, ct);

        string? oldFilename = null;

        List<string> dependencies =
            (info.Version.Dependencies ?? [])
            .Where(v => v.DependencyType == Modrinth.Models.Enums.Version.DependencyType.Required)
            .Select(v => v.ProjectId)
            .Where(v => v is not null)
            .ToList()!;

        if (info.LockEntry is null)
        {
            settings.ModlistLock.Add(new ModLock()
            {
                Id = info.Mod.Id,
                Hash = info.File.Hashes.Sha1,
                FileName = info.DownloadFileName,
                ReleasedOn = info.Version.DatePublished.ToString("o"),
                DownloadUrl = info.File.Url,
                Dependencies = dependencies,
            });
        }
        else
        {
            if (info.LockEntry.FileName != info.DownloadFileName)
            {
                oldFilename = info.LockEntry.FileName;
            }

            info.LockEntry.Hash = info.File.Hashes.Sha1;
            info.LockEntry.FileName = info.DownloadFileName;
            info.LockEntry.ReleasedOn = info.Version.DatePublished.ToString("o");
            info.LockEntry.DownloadUrl = info.File.Url;
            info.LockEntry.Dependencies.Clear();
            info.LockEntry.Dependencies.AddRange(dependencies);
        }

        await File.WriteAllTextAsync(Settings.ModlistLockPath, JsonSerializer.Serialize(settings.ModlistLock, Utils.JsonSerializerOptions), ct);

        oldFilename = oldFilename is null ? null : Path.GetFullPath(Path.Combine(settings.ModsDirectory, oldFilename));

        if (oldFilename is not null && File.Exists(oldFilename))
        {
            File.Delete(oldFilename);
        }
    }

    public static void UninstallMod(ModUninstallInfo info, Settings settings)
    {
        if (info.LockEntry is not null)
        {
            settings.ModlistLock.Remove(info.LockEntry);
        }

        if (info.File is not null)
        {
            File.Delete(info.File);
        }
    }

    public static async Task<ModDownloadInfo> GetModDownload(string id, Settings settings, ModrinthClient client, CancellationToken ct)
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

        foreach (var version in versions)
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
                Mod = settings.Modlist.Mods.FirstOrDefault(v => v.Id == id) ?? new ModEntry() { Id = id },
                File = file,
                Version = version,
                LockEntry = lockfileEntry,
                DownloadFileName = downloadFileName,
            };
        }

        throw new ModNotSupported($"Mod {settings.GetModName(id) ?? id} not supported");
    }

    public static async Task<ModUpdate?> GetModIfNeeded(string id, Settings settings, ModrinthClient client, CancellationToken ct)
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
            ? new ModUpdate()
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

    static readonly SearchValues<char> searchValues = SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

    public static bool IsModrinthId(string value) => value.Length == 8 && value.All(searchValues.Contains);

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

        if (string.Equals(result.Title, query, StringComparison.InvariantCultureIgnoreCase))
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

    static async Task<(bool NeedsDownload, ModUpdateReason Reason)> IsNeeded(ModEntry mod, Settings settings, CancellationToken ct)
    {
        ModLock? lockfileEntry = settings.ModlistLock.FirstOrDefault(v => v.Id == mod.Id);
        if (lockfileEntry is null)
        {
            return (true, ModUpdateReason.NotInstalled);
        }

        string filename = Path.GetFullPath(Path.Combine(settings.ModsDirectory, lockfileEntry.FileName));
        if (!File.Exists(filename))
        {
            return (true, ModUpdateReason.NotInstalled);
        }

        string hash = await Utils.ComputeHash1(filename, ct);
        if (hash != lockfileEntry.Hash)
        {
            return (true, ModUpdateReason.InvalidHash);
        }

        return (false, default);
    }

    public static async Task<(List<ModUpdate> Updates, List<ModUninstall> Uninstalls)> CheckChanges(Settings settings, ModrinthClient client, CancellationToken ct)
    {
        Log.Section($"Checking for changes");

        List<ModUpdate> modUpdates = [];
        List<ModUninstall> modUninstalls = [];

        {
            Log.MinorAction($"Checking for new mods");

            List<(ModEntry Mod, ModUpdateReason Reason)> fetchThese = [];

            foreach (var mod in settings.Modlist.Mods)
            {
                var (needsDownload, reason) = await IsNeeded(mod, settings, ct);
                if (!needsDownload) continue;
                fetchThese.Add((mod, reason));
            }

            Log.MinorAction($"Fetching mods");

            ProgressBar progressBar = new();

            for (int i = 0; i < fetchThese.Count; i++)
            {
                var mod = fetchThese[i];

                progressBar.Report(mod.Mod.Name ?? mod.Mod.Id, i, fetchThese.Count);

                ModDownloadInfo? update;

                try
                {
                    update = await Install.GetModDownload(mod.Mod.Id, settings, client, ct);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    continue;
                }

                if (update is null) continue;

                modUpdates.Add(new ModUpdate()
                {
                    Reason = mod.Reason,
                    Mod = mod.Mod,
                    DownloadFileName = update.DownloadFileName,
                    File = update.File,
                    LockEntry = update.LockEntry,
                    Version = update.Version,
                });

                foreach (var dependency in update.Version.Dependencies ?? [])
                {
                    if (dependency.ProjectId is null) continue;
                    if (fetchThese.Any(v => v.Mod.Id == dependency.ProjectId)) continue;

                    switch (dependency.DependencyType)
                    {
                        case Modrinth.Models.Enums.Version.DependencyType.Required:
                            ModEntry entry = settings.Modlist.Mods.FirstOrDefault(v => v.Id == dependency.ProjectId) ?? new ModEntry()
                            {
                                Id = dependency.ProjectId,
                            };
                            var (needsDownload, reason) = await IsNeeded(entry, settings, ct);
                            if (!needsDownload) break;
                            fetchThese.Add((entry, reason));
                            break;
                        case Modrinth.Models.Enums.Version.DependencyType.Optional:
                            break;
                        case Modrinth.Models.Enums.Version.DependencyType.Incompatible:
                            break;
                        case Modrinth.Models.Enums.Version.DependencyType.Embedded:
                            break;
                    }
                }
            }

            progressBar.Dispose();

            Log.MinorAction($"Checking for orphan mods");

            foreach (var modlock in settings.ModlistLock)
            {
                var mod = settings.Modlist.Mods.FirstOrDefault(v => v.Id == modlock.Id);

                if (mod is not null) continue;

                string? file = Path.GetFullPath(Path.Combine(settings.ModsDirectory, modlock.FileName));

                if (!File.Exists(file))
                {
                    file = null;
                }

                modUninstalls.Add(new ModUninstall()
                {
                    Reason = ModUninstallReason.Because,
                    File = file,
                    LockEntry = modlock,
                });
            }

            void DontUninstall(ModLock modlock)
            {
                foreach (string dep in modlock.Dependencies)
                {
                    var dependencyLock = settings.ModlistLock.FirstOrDefault(v => v.Id == dep);
                    if (dependencyLock is null) continue;
                    var uninstall = modUninstalls.FirstOrDefault(v => v.LockEntry is not null && v.LockEntry.Id == dep);
                    if (uninstall is not null) modUninstalls.Remove(uninstall);
                    DontUninstall(dependencyLock);
                }
            }

            foreach (var modlock in settings.ModlistLock)
            {
                if (modUninstalls.Any(v => v.LockEntry is not null && v.LockEntry.Id == modlock.Id)) continue;
                DontUninstall(modlock);
            }

            Log.MinorAction($"Fetching missing mod metadata");

            progressBar = new();

            List<string> modsWithMissingMetadata = [
                .. settings.ModlistLock.Select(v => v.Id),
            ];
            modsWithMissingMetadata.AddRange(modUpdates.Where(v => v.LockEntry is not null).Select(v => v.LockEntry!.Id).Where(v => v is not null && !modsWithMissingMetadata.Any(w => v == w))!);
            modsWithMissingMetadata.AddRange(modUninstalls.Where(v => v.LockEntry is not null).Select(v => v.LockEntry!.Id).Where(v => v is not null && !modsWithMissingMetadata.Any(w => v == w))!);
            modsWithMissingMetadata.AddRange(settings.Modlist.Mods.Select(v => v.Id).Where(v => v is not null && !modsWithMissingMetadata.Any(w => v == w))!);

            modsWithMissingMetadata.RemoveAll(v => settings.GetModName(v) is not null);

            for (int i = 0; i < modsWithMissingMetadata.Count; i++)
            {
                string? mod = modsWithMissingMetadata[i];

                progressBar.Report(mod, i, modsWithMissingMetadata.Count);

                try
                {
                    Modrinth.Models.Project project = await client.Project.GetAsync(mod, ct);

                    var lockEntry = settings.ModlistLock.FirstOrDefault(v => v.Id == mod);
                    if (lockEntry is not null)
                    {
                        lockEntry.Name = project.Title;
                        await File.WriteAllTextAsync(Settings.ModlistLockPath, JsonSerializer.Serialize(settings.ModlistLock, Utils.JsonSerializerOptions), ct);
                    }

                    var modEntry = settings.Modlist.Mods.FirstOrDefault(v => v.Id == mod);
                    if (modEntry is not null) modEntry.Name = project.Title;
                }
                catch (ModrinthApiException ex)
                {
                    Log.Error($"Failed to fetch mod {mod} ({ex.Response?.ReasonPhrase ?? ex.Error?.Error ?? ex.Message})");
                }
            }

            progressBar.Dispose();
        }

        return (modUpdates, modUninstalls);
    }

    public static async Task PerformChanges(Settings settings, List<ModUpdate> modUpdates, List<ModUninstall> modUninstalls, CancellationToken ct)
    {
        ImmutableArray<InstalledMod> mods = await settings.ReadMods(ct);

        {
            string GetName1(ModUpdate update) => settings.GetModName(update.Mod.Id) ??
                $"{update.LockEntry?.Name ?? update.DownloadFileName} ({update.Mod.Id})";

            string GetName2(ModUninstall uninstall) => settings.GetModName(uninstall.LockEntry?.Id) ??
                (uninstall.File is not null && uninstall.LockEntry is not null
                    ? $"{uninstall.File} ({uninstall.LockEntry.Id})"
                    : $"{uninstall.File ?? uninstall.LockEntry?.Id}");

            int nameMaxWidth = 0;
            nameMaxWidth = modUpdates.Aggregate(nameMaxWidth, (a, v) => Math.Max(a, GetName1(v).Length));
            nameMaxWidth = modUninstalls.Aggregate(nameMaxWidth, (a, v) => Math.Max(a, GetName2(v).Length));
            nameMaxWidth += 2;

            if (modUpdates.Count > 0 || modUninstalls.Count > 0)
            {
                Console.WriteLine();
            }

            foreach (ModUpdate update in modUpdates)
            {
                string name = GetName1(update);

                switch (update.Reason)
                {
                    case ModUpdateReason.NewVersion:
                    case ModUpdateReason.HashChanged:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("^ ");
                        break;
                    case ModUpdateReason.InvalidHash:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("^ ");
                        break;
                    case ModUpdateReason.NotInstalled:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("+ ");
                        break;
                }
                Console.ResetColor();

                Console.Write(name);
                Console.Write(new string(' ', nameMaxWidth - name.Length));

                var existing = update.LockEntry is null ? null : mods.FirstOrDefault(v => Path.GetFileName(v.FileName) == update.LockEntry.FileName).Mod;

                if (existing is not null)
                {
                    Console.Write($"({existing.Version} -> {update.Version.VersionNumber})");
                }
                else
                {
                    Console.Write($"({update.Version.VersionNumber})");
                }

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($" ({update.Version.Name})");
                Console.ResetColor();

                Console.WriteLine();
            }

            foreach (ModUninstall uninstall in modUninstalls)
            {
                string name = GetName2(uninstall);

                switch (uninstall.Reason)
                {
                    case ModUninstallReason.Because:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("x ");
                        break;
                    default:
                        break;
                }
                Console.ResetColor();

                Console.Write(name);

                Console.WriteLine();
            }
        }

        if (modUpdates.Count == 0 && modUninstalls.Count == 0)
        {
            Log.Info($"No actions have to be done");
        }
        else
        {
            if (Log.AskYesNo("Do you want to perform the actions above?", true))
            {
                await File.WriteAllTextAsync(Settings.ModlistPath, JsonSerializer.Serialize(settings.Modlist, Utils.JsonSerializerOptions), ct);

                ProgressBar progressBar = new();

                for (int i = 0; i < modUpdates.Count; i++)
                {
                    ModUpdate update = modUpdates[i];

                    progressBar.Report(update.LockEntry?.Name ?? update.Mod.Id, i, modUpdates.Count);

                    await Install.DownloadMod(update, settings, ct);
                }

                progressBar.Dispose();

                for (int i = 0; i < modUninstalls.Count; i++)
                {
                    var uninstall = modUninstalls[i];

                    Install.UninstallMod(uninstall, settings);
                    await File.WriteAllTextAsync(Settings.ModlistLockPath, JsonSerializer.Serialize(settings.ModlistLock, Utils.JsonSerializerOptions), ct);
                }
            }
        }
    }
}
