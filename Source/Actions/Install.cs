using System.Collections.Immutable;
using System.Text.Json;
using Modrinth;
using Modrinth.Exceptions;

namespace MMM.Actions;

public class ModDownloadInfo
{
    public required Modrinth.Models.Version Version { get; init; }
    public required Modrinth.Models.File File { get; init; }
    public required ModlistLockEntry? LockEntry { get; init; }
    public required string DownloadFileName { get; init; }
    public required ModlistEntry Mod { get; init; }
}

public enum ModUninstallReason
{
    Because,
}

public class ModUninstallInfo
{
    public required string? File { get; init; }
    public required ModlistLockEntry? LockEntry { get; init; }
}

public static class Install
{
    public static async Task DownloadMod(ModDownloadInfo info, List<ModlistLockEntry> modlistLock, string modsDirectory, CancellationToken ct)
    {
        using var httpClient = new HttpClient();
        using var s = await httpClient.GetStreamAsync(info.File.Url, ct);
        using var fs = new FileStream(Path.GetFullPath(Path.Combine(modsDirectory, info.DownloadFileName)), FileMode.Create);
        await s.CopyToAsync(fs, ct);

        string? oldFilename = null;

        if (info.LockEntry is null)
        {
            modlistLock.Add(new ModlistLockEntry()
            {
                Id = info.Mod.Id,
                Hash = info.File.Hashes.Sha1,
                FileName = info.DownloadFileName,
                ReleasedOn = info.Version.DatePublished.ToString("o"),
                DownloadUrl = info.File.Url,
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
        }

        await File.WriteAllTextAsync(Settings.ModlistLockPath, JsonSerializer.Serialize(modlistLock, Utils.JsonSerializerOptions), ct);

        oldFilename = oldFilename is null ? null : Path.GetFullPath(Path.Combine(modsDirectory, oldFilename));

        if (oldFilename is not null && File.Exists(oldFilename))
        {
            File.Delete(oldFilename);
        }
    }

    public static void UninstallMod(ModUninstallInfo info, List<ModlistLockEntry> modlistLock, string modsDirectory)
    {
        if (info.LockEntry is not null)
        {
            int j = modlistLock.IndexOf(v => v.Id == info.LockEntry.Id);
            modlistLock.RemoveAt(j);
        }

        if (info.File is not null)
        {
            File.Delete(info.File);
        }
    }

    public static async Task<ModDownloadInfo> FindMod(string id, Settings settings, ModrinthClient client, CancellationToken ct)
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

            ModlistLockEntry? lockfileEntry = settings.ModlistLock.FirstOrDefault(v => v.Id == id);

            return new ModDownloadInfo()
            {
                Mod = settings.Modlist.Mods.FirstOrDefault(v => v.Id == id) ?? new ModlistEntry() { Id = id },
                File = file,
                Version = version,
                LockEntry = lockfileEntry,
                DownloadFileName = downloadFileName,
            };
        }

        throw new ModNotSupported($"Mod {id} not supported");
    }

    public static async Task<ModUpdate?> FindModIfNeeded(string id, Settings settings, ModrinthClient client, CancellationToken ct)
    {
        ModDownloadInfo downloadInfo = await FindMod(id, settings, client, ct);

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
                Mod = settings.Modlist.Mods.FirstOrDefault(v => v.Id == id) ?? new ModlistEntry() { Id = id },
                DownloadFileName = downloadInfo.DownloadFileName,
                File = downloadInfo.File,
                LockEntry = downloadInfo.LockEntry,
                Version = downloadInfo.Version,
            }
            : null;
    }

    public static async Task<(List<ModUpdate> Updates, List<ModUninstall> Uninstalls)> CheckChanges(Settings settings, ModrinthClient client, CancellationToken ct)
    {
        List<ModUpdate> modUpdates = [];
        List<ModUninstall> modUninstalls = [];

        {
            Log.SubAction($"Checking for new mods ...");

            List<(ModlistEntry Mod, ModUpdateReason Reason)> fetchThese = [];

            foreach (var mod in settings.Modlist.Mods)
            {
                var lockfileEntry = settings.ModlistLock.FirstOrDefault(v => v.Id == mod.Id);

                bool needsDownload = false;
                ModUpdateReason reason = default;

                if (lockfileEntry is null)
                {
                    needsDownload = true;
                    reason = ModUpdateReason.NotInstalled;
                }
                else
                {
                    string filename = Path.GetFullPath(Path.Combine(settings.ModsDirectory, lockfileEntry.FileName));

                    if (File.Exists(filename))
                    {
                        string hash = await Utils.ComputeHash1(filename, ct);
                        if (hash != lockfileEntry.Hash)
                        {
                            needsDownload = true;
                            reason = ModUpdateReason.InvalidHash;
                        }
                    }
                    else
                    {
                        needsDownload = true;
                        reason = ModUpdateReason.NotInstalled;
                    }
                }

                if (needsDownload)
                {
                    fetchThese.Add((mod, reason));
                }
            }

            LogEntry lastLine = default;
            Log.Keep(lastLine);

            for (int i = 0; i < fetchThese.Count; i++)
            {
                var mod = fetchThese[i];

                lastLine.Back();
                Log.Rekeep(lastLine =
                    Log.Write(mod.Mod.Name ?? mod.Mod.Id) +
                    Log.Write(new string(' ', Math.Max(0, Console.WindowWidth / 2 - (mod.Mod.Name ?? mod.Mod.Id).Length))) +
                    Log.Progress(i, fetchThese.Count));

                ModDownloadInfo? update;

                try
                {
                    update = await Install.FindMod(mod.Mod.Id, settings, client, ct);
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
            }

            lastLine.Clear();
            Log.Unkeep();

            Log.SubAction($"Checking for orphan mods ...");

            for (int i = 0; i < settings.ModlistLock.Count; i++)
            {
                var modlock = settings.ModlistLock[i];
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

            Log.SubAction($"Fetching missing mod metadata ...");

            for (int i = 0; i < settings.Modlist.Mods.Count; i++)
            {
                ModlistEntry mod = settings.Modlist.Mods[i];
                if (mod.Name is not null) continue;

                try
                {
                    Modrinth.Models.Project project = await client.Project.GetAsync(mod.Id, ct);
                    mod.Name = project.Title;
                }
                catch
                {

                }
            }
        }

        return (modUpdates, modUninstalls);
    }

    public static async Task PerformChanges(Settings settings, List<ModUpdate> modUpdates, List<ModUninstall> modUninstalls, CancellationToken ct)
    {
        ImmutableArray<InstalledMod> mods = await settings.ReadMods(ct);

        {
            static string GetName1(ModUpdate update) =>
                $"{update.Mod.Name ?? update.DownloadFileName} ({update.Mod.Id})";

            static string GetName2(ModUninstall uninstall) =>
                uninstall.File is not null && uninstall.LockEntry is not null
                    ? $"{uninstall.File} ({uninstall.LockEntry.Id})"
                    : $"{uninstall.File ?? uninstall.LockEntry?.Id}";

            int nameMaxWidth = 0;
            nameMaxWidth = modUpdates.Aggregate(nameMaxWidth, (a, v) => Math.Max(a, GetName1(v).Length));
            nameMaxWidth = modUninstalls.Aggregate(nameMaxWidth, (a, v) => Math.Max(a, GetName2(v).Length));
            nameMaxWidth += 2;

            foreach (ModUpdate update in modUpdates)
            {
                string name = GetName1(update);

                switch (update.Reason)
                {
                    case ModUpdateReason.NewVersion:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("^ ");
                        break;
                    case ModUpdateReason.HashChanged:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("^ ");
                        break;
                    case ModUpdateReason.NotInstalled:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("+ ");
                        break;
                    case ModUpdateReason.InvalidHash:
                        Console.ForegroundColor = ConsoleColor.Yellow;
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
            Log.Notice($"No actions have to be done");
        }
        else
        {
            if (Log.AskYesNo("Do you want to perform the actions above? [y/n]"))
            {
                await File.WriteAllTextAsync(Settings.ModlistPath, JsonSerializer.Serialize(settings.Modlist, Utils.JsonSerializerOptions), ct);

                LogEntry lastLine = default;
                Log.Keep(lastLine);

                for (int i = 0; i < modUpdates.Count; i++)
                {
                    ModUpdate update = modUpdates[i];

                    string name = update.Mod.Name ?? update.Mod.Id;

                    lastLine.Back();
                    Log.Rekeep(lastLine =
                        Log.Write(name) +
                        Log.Write(new string(' ', Math.Max(0, Console.WindowWidth / 2 - name.Length))) +
                        Log.Progress(i, modUpdates.Count));

                    await Install.DownloadMod(update, settings.ModlistLock, settings.ModsDirectory, ct);
                }

                lastLine.Clear();
                Log.Unkeep();

                for (int i = 0; i < modUninstalls.Count; i++)
                {
                    var uninstall = modUninstalls[i];

                    Install.UninstallMod(uninstall, settings.ModlistLock, settings.ModsDirectory);
                    await File.WriteAllTextAsync(Settings.ModlistLockPath, JsonSerializer.Serialize(settings.ModlistLock, Utils.JsonSerializerOptions), ct);
                }
            }
        }
    }
}
