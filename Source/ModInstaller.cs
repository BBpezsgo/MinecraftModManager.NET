using System.Collections.Immutable;
using System.Text.Json;
using Modrinth;
using Modrinth.Exceptions;

namespace MMM;

public static class ModInstaller
{
    public static async Task<(List<ModDownloadInfo> Updates, List<ModUninstallInfo> Uninstalls)> CheckChanges(Context settings, ModrinthClient client, CancellationToken ct)
    {
        Log.Section($"Checking for changes");

        List<ModDownloadInfo> modUpdates = [];
        List<ModUninstallInfo> modUninstalls = [];

        {
            Log.MinorAction($"Checking for new mods");

            List<(ModEntry Mod, ModUpdateReason Reason)> fetchThese = [];

            foreach (ModEntry mod in settings.Modlist.Mods)
            {
                (bool needsDownload, ModUpdateReason reason) = await IsNeeded(mod, settings, ct);
                if (!needsDownload) continue;
                fetchThese.Add((mod, reason));
            }

            if (fetchThese.Count > 0)
            {
                Log.MinorAction($"Fetching mods");

                ProgressBar progressBar = new();

                for (int i = 0; i < fetchThese.Count; i++)
                {
                    (ModEntry mod, ModUpdateReason reason) = fetchThese[i];

                    progressBar.Report(mod.Name ?? mod.Id, i, fetchThese.Count);

                    ModDownloadInfo? update;

                    try
                    {
                        update = await ModrinthUtils.GetModDownload(mod.Id, settings, client, ct);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                        continue;
                    }

                    if (update is null) continue;

                    modUpdates.Add(new ModDownloadInfo()
                    {
                        Reason = reason,
                        Mod = mod,
                        DownloadFileName = update.DownloadFileName,
                        File = update.File,
                        LockEntry = update.LockEntry,
                        Version = update.Version,
                    });

                    foreach (Modrinth.Models.Dependency dependency in update.Version.Dependencies ?? [])
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
                                (bool needsDownload, reason) = await IsNeeded(entry, settings, ct);
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
            }

            Log.MinorAction($"Checking for orphan mods");

            foreach (ModLock modlock in settings.ModlistLock)
            {
                ModEntry? mod = settings.Modlist.Mods.FirstOrDefault(v => v.Id == modlock.Id);

                if (mod is not null) continue;

                string? file = Path.GetFullPath(Path.Combine(settings.ModsDirectory, modlock.FileName));

                if (!File.Exists(file))
                {
                    file = null;
                }

                modUninstalls.Add(new ModUninstallInfo()
                {
                    Reason = ModUninstallReason.Orphan,
                    File = file,
                    LockEntry = modlock,
                });
            }

            void DontUninstall(ModLock modlock)
            {
                foreach (string dep in modlock.Dependencies)
                {
                    ModLock? dependencyLock = settings.ModlistLock.FirstOrDefault(v => v.Id == dep);
                    if (dependencyLock is null) continue;
                    ModUninstallInfo? uninstall = modUninstalls.FirstOrDefault(v => v.LockEntry is not null && v.LockEntry.Id == dep);
                    if (uninstall is not null) modUninstalls.Remove(uninstall);
                    DontUninstall(dependencyLock);
                }
            }

            foreach (ModLock modlock in settings.ModlistLock)
            {
                if (modUninstalls.Any(v => v.LockEntry is not null && v.LockEntry.Id == modlock.Id)) continue;
                DontUninstall(modlock);
            }

            List<string> modsWithMissingMetadata = [
                .. settings.ModlistLock.Select(v => v.Id),
            ];
            modsWithMissingMetadata.AddRange(modUpdates.Where(v => v.LockEntry is not null).Select(v => v.LockEntry!.Id).Where(v => v is not null && !modsWithMissingMetadata.Any(w => v == w))!);
            modsWithMissingMetadata.AddRange(modUninstalls.Where(v => v.LockEntry is not null).Select(v => v.LockEntry!.Id).Where(v => v is not null && !modsWithMissingMetadata.Any(w => v == w))!);
            modsWithMissingMetadata.AddRange(settings.Modlist.Mods.Select(v => v.Id).Where(v => v is not null && !modsWithMissingMetadata.Any(w => v == w))!);

            modsWithMissingMetadata.RemoveAll(v => settings.GetModName(v) is not null);

            if (modsWithMissingMetadata.Count > 0)
            {
                Log.MinorAction($"Fetching missing mod metadata");

                ProgressBar progressBar = new();

                for (int i = 0; i < modsWithMissingMetadata.Count; i++)
                {
                    string? mod = modsWithMissingMetadata[i];

                    progressBar.Report(mod, i, modsWithMissingMetadata.Count);

                    try
                    {
                        Modrinth.Models.Project project = await client.Project.GetAsync(mod, ct);

                        ModLock? lockEntry = settings.ModlistLock.FirstOrDefault(v => v.Id == mod);
                        if (lockEntry is not null)
                        {
                            lockEntry.Name = project.Title;
                            await File.WriteAllTextAsync(Context.ModlistLockPath, JsonSerializer.Serialize(settings.ModlistLock, Utils.JsonSerializerOptions), ct);
                        }

                        ModEntry? modEntry = settings.Modlist.Mods.FirstOrDefault(v => v.Id == mod);
                        if (modEntry is not null) modEntry.Name = project.Title;
                    }
                    catch (ModrinthApiException ex)
                    {
                        Log.Error($"Failed to fetch mod {mod} ({ex.Response?.ReasonPhrase ?? ex.Error?.Error ?? ex.Message})");
                    }
                }

                progressBar.Dispose();
            }
        }

        return (modUpdates, modUninstalls);
    }

    static async Task<(bool NeedsDownload, ModUpdateReason Reason)> IsNeeded(ModEntry mod, Context settings, CancellationToken ct)
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

    public static async Task DownloadMod(ModDownloadInfo info, Context settings, CancellationToken ct)
    {
        if (!Directory.Exists(settings.ModsDirectory))
        {
            Directory.CreateDirectory(settings.ModsDirectory);
        }

        string filepath = Path.GetFullPath(Path.Combine(settings.ModsDirectory, info.DownloadFileName));
        using HttpClient httpClient = new();
        using Stream s = await httpClient.GetStreamAsync(info.File.Url, ct);
        using FileStream fs = new(filepath, FileMode.Create);
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

        await File.WriteAllTextAsync(Context.ModlistLockPath, JsonSerializer.Serialize(settings.ModlistLock, Utils.JsonSerializerOptions), ct);

        oldFilename = oldFilename is null ? null : Path.GetFullPath(Path.Combine(settings.ModsDirectory, oldFilename));

        if (oldFilename is not null && File.Exists(oldFilename))
        {
            File.Delete(oldFilename);
        }
    }

    public static void UninstallMod(ModUninstallInfo info, Context settings)
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

    public static async Task PerformChanges(Context settings, List<ModDownloadInfo> modUpdates, List<ModUninstallInfo> modUninstalls, CancellationToken ct)
    {
        ImmutableArray<InstalledMod> mods = await settings.GetMods(ct);

        {
            string GetName1(ModDownloadInfo update)
            {
                return settings.GetModName(update.Mod.Id) ??
                    $"{update.LockEntry?.Name ?? update.DownloadFileName} ({update.Mod.Id})";
            }

            string GetName2(ModUninstallInfo uninstall)
            {
                return settings.GetModName(uninstall.LockEntry?.Id) ??
                    (uninstall.File is not null && uninstall.LockEntry is not null
                    ? $"{uninstall.File} ({uninstall.LockEntry.Id})"
                    : $"{uninstall.File ?? uninstall.LockEntry?.Id}");
            }

            int nameMaxWidth = 0;
            nameMaxWidth = modUpdates.Aggregate(nameMaxWidth, (a, v) => Math.Max(a, GetName1(v).Length));
            nameMaxWidth = modUninstalls.Aggregate(nameMaxWidth, (a, v) => Math.Max(a, GetName2(v).Length));
            nameMaxWidth += 2;

            if (modUpdates.Count > 0 || modUninstalls.Count > 0)
            {
                Console.WriteLine();
            }

            foreach (ModDownloadInfo update in modUpdates)
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

                Fabric.FabricMod? existing = update.LockEntry is null ? null : mods.FirstOrDefault(v => Path.GetFileName(v.FileName) == update.LockEntry.FileName).Mod;

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

            foreach (ModUninstallInfo uninstall in modUninstalls)
            {
                string name = GetName2(uninstall);

                switch (uninstall.Reason)
                {
                    case ModUninstallReason.Because:
                    case ModUninstallReason.NotSupported:
                    case ModUninstallReason.Orphan:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("x ");
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
                await File.WriteAllTextAsync(Context.ModlistPath, JsonSerializer.Serialize(settings.Modlist, Utils.JsonSerializerOptions), ct);

                ProgressBar progressBar = new();

                for (int i = 0; i < modUpdates.Count; i++)
                {
                    ModDownloadInfo update = modUpdates[i];

                    progressBar.Report(update.LockEntry?.Name ?? update.Mod.Id, i, modUpdates.Count);

                    await DownloadMod(update, settings, ct);
                }

                progressBar.Dispose();

                for (int i = 0; i < modUninstalls.Count; i++)
                {
                    ModUninstallInfo uninstall = modUninstalls[i];

                    UninstallMod(uninstall, settings);
                    await File.WriteAllTextAsync(Context.ModlistLockPath, JsonSerializer.Serialize(settings.ModlistLock, Utils.JsonSerializerOptions), ct);
                }
            }
        }
    }
}
