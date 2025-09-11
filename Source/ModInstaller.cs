using System.Collections.Immutable;
using System.Text.Json;
using Logger;
using Modrinth.Exceptions;

namespace MMM;

public static class ModInstaller
{
    public static async Task<Changes> CheckChanges(CancellationToken ct)
    {
        Log.Section($"Checking for changes");

        ImmutableArray<ModInstallInfo>.Builder modUpdates = ImmutableArray.CreateBuilder<ModInstallInfo>();
        ImmutableArray<ModUninstallInfo>.Builder modUninstalls = ImmutableArray.CreateBuilder<ModUninstallInfo>();
        List<ModEntry> unsupportedMods = [];

        {
            Log.MinorAction($"Checking for new mods");

            List<(ModEntry Mod, ModUpdateReason Reason)> fetchThese = [];

            foreach (ModEntry mod in Context.Instance.Modlist.Mods)
            {
                (bool needsDownload, ModUpdateReason reason) = await IsNeeded(mod, ct);
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

                    ModInstallInfo? update;

                    try
                    {
                        update = await ModrinthUtils.GetModDownload(mod.Id, ct);
                    }
                    catch (ModNotSupported e)
                    {
                        unsupportedMods.Add(mod);
                        Log.Error(e);
                        continue;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                        continue;
                    }

                    if (update is null) continue;

                    modUpdates.Add(new ModInstallInfo()
                    {
                        Reason = reason,
                        Mod = mod,
                        DownloadFileName = update.DownloadFileName,
                        File = update.File,
                        LockEntry = update.LockEntry,
                        Version = update.Version,
                        Name = update.Name,
                    });

                    foreach (Modrinth.Models.Dependency dependency in update.Version.Dependencies ?? [])
                    {
                        if (dependency.ProjectId is null) continue;
                        if (fetchThese.Any(v => v.Mod.Id == dependency.ProjectId)) continue;

                        switch (dependency.DependencyType)
                        {
                            case Modrinth.Models.Enums.Version.DependencyType.Required:
                                ModEntry entry = Context.Instance.Modlist.Mods.FirstOrDefault(v => v.Id == dependency.ProjectId) ?? new ModEntry()
                                {
                                    Id = dependency.ProjectId,
                                    Name = await ModrinthUtils.GetModName(dependency.ProjectId, ct),
                                };
                                (bool needsDownload, reason) = await IsNeeded(entry, ct);
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

            if (unsupportedMods.Count > 0 && Log.AskYesNo($"Do you want to remove {unsupportedMods.Count} unsupported mods?", true))
            {
                foreach (ModEntry mod in unsupportedMods)
                {
                    if (modUninstalls.Any(v => v.LockEntry?.Id == mod.Id)) continue;

                    string name = await ModrinthUtils.GetModName(mod.Id, ct);

                    foreach (ModLock other in Context.Instance.ModlistLock)
                    {
                        if (other.Dependencies.Contains(mod.Id))
                        {
                            Log.Error($"Mod {name} is unsupported but required by mod {await ModrinthUtils.GetModName(other.Id, ct)}");
                        }
                    }

                    Context.Instance.Modlist.Mods.Remove(mod);
                    ModLock? modLock = Context.Instance.ModlistLock.FirstOrDefault(v => v.Id == (string?)mod.Id);
                    if (modLock is not null)
                    {
                        string file = Context.Instance.GetModPath(modLock);
                        modUninstalls.Add(new ModUninstallInfo()
                        {
                            Reason = ModUninstallReason.NotSupported,
                            LockEntry = modLock,
                            File = File.Exists(file) ? file : null,
                            Name = name,
                        });
                    }
                    else
                    {
                        modUninstalls.Add(new ModUninstallInfo()
                        {
                            Reason = ModUninstallReason.NotSupported,
                            LockEntry = null,
                            File = null,
                            Name = name,
                        });
                    }
                }
            }

            Log.MinorAction($"Checking for orphan mods");

            foreach (ModLock modlock in Context.Instance.ModlistLock)
            {
                if (Context.Instance.Modlist.Mods.Any(v => v.Id == modlock.Id)) continue;

                string? file = Context.Instance.GetModPath(modlock);

                if (!File.Exists(file))
                {
                    file = null;
                }

                modUninstalls.Add(new ModUninstallInfo()
                {
                    Reason = ModUninstallReason.Orphan,
                    File = file,
                    LockEntry = modlock,
                    Name = await ModrinthUtils.GetModName(modlock.Id, ct),
                });
            }

            void DontUninstall(ModLock modlock)
            {
                foreach (string dep in modlock.Dependencies)
                {
                    ModLock? dependencyLock = Context.Instance.ModlistLock.FirstOrDefault(v => v.Id == dep);
                    if (dependencyLock is null) continue;
                    ModUninstallInfo? uninstall = modUninstalls.FirstOrDefault(v => v.LockEntry is not null && v.LockEntry.Id == dep);
                    if (uninstall is not null) modUninstalls.Remove(uninstall);
                    DontUninstall(dependencyLock);
                }
            }

            foreach (ModLock modlock in Context.Instance.ModlistLock)
            {
                if (modUninstalls.Any(v => v.LockEntry is not null && v.LockEntry.Id == modlock.Id)) continue;
                DontUninstall(modlock);
            }

            List<string> modsWithMissingMetadata = [
                .. Context.Instance.ModlistLock.Select(v => v.Id),
            ];
            modsWithMissingMetadata.AddRange(modUpdates.Where(v => v.LockEntry is not null).Select(v => v.LockEntry!.Id).Where(v => v is not null && !modsWithMissingMetadata.Any(w => v == w))!);
            modsWithMissingMetadata.AddRange(modUninstalls.Where(v => v.LockEntry is not null).Select(v => v.LockEntry!.Id).Where(v => v is not null && !modsWithMissingMetadata.Any(w => v == w))!);
            modsWithMissingMetadata.AddRange(Context.Instance.Modlist.Mods.Select(v => v.Id).Where(v => v is not null && !modsWithMissingMetadata.Any(w => v == w))!);

            modsWithMissingMetadata.RemoveAll(v => Context.Instance.GetModName(v) is not null);

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
                        string name = await ModrinthUtils.GetModNameForce(mod, ct);

                        ModLock? lockEntry = Context.Instance.ModlistLock.FirstOrDefault(v => v.Id == mod);
                        if (lockEntry is not null)
                        {
                            lockEntry.Name = name;
                            await File.WriteAllTextAsync(Context.ModlistLockPath, JsonSerializer.Serialize(Context.Instance.ModlistLock, ModLockListJsonSerializerContext.Default.ListModLock), ct);
                        }

                        ModEntry? modEntry = Context.Instance.Modlist.Mods.FirstOrDefault(v => v.Id == mod);
                        if (modEntry is not null) modEntry.Name = name;
                    }
                    catch (ModrinthApiException ex)
                    {
                        Log.Error($"Failed to fetch mod {mod} ({ex.Response?.ReasonPhrase ?? ex.Error?.Error ?? ex.Message})");
                    }
                }

                progressBar.Dispose();
            }
        }

        return new Changes()
        {
            Install = modUpdates.ToImmutable(),
            Uninstall = modUninstalls.ToImmutable(),
        };
    }

    static async Task<(bool NeedsDownload, ModUpdateReason Reason)> IsNeeded(ModEntry mod, CancellationToken ct)
    {
        ModLock? lockfileEntry = Context.Instance.ModlistLock.FirstOrDefault(v => v.Id == mod.Id);
        if (lockfileEntry is null)
        {
            return (true, ModUpdateReason.NotInstalled);
        }

        string filename = Context.Instance.GetModPath(lockfileEntry);
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

    public static async Task DownloadMod(ModInstallInfo info, CancellationToken ct)
    {
        if (!Directory.Exists(Context.Instance.ModsDirectory))
        {
            Directory.CreateDirectory(Context.Instance.ModsDirectory);
        }

        string filepath = Path.GetFullPath(Path.Combine(Context.Instance.ModsDirectory, info.DownloadFileName));
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
            Context.Instance.ModlistLock.Add(new ModLock()
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

        await File.WriteAllTextAsync(Context.ModlistLockPath, JsonSerializer.Serialize(Context.Instance.ModlistLock, ModLockListJsonSerializerContext.Default.ListModLock), ct);

        oldFilename = oldFilename is null ? null : Path.GetFullPath(Path.Combine(Context.Instance.ModsDirectory, oldFilename));

        if (oldFilename is not null && File.Exists(oldFilename))
        {
            File.Delete(oldFilename);
        }
    }

    public static void UninstallMod(ModUninstallInfo info)
    {
        if (info.LockEntry is not null)
        {
            Context.Instance.ModlistLock.Remove(info.LockEntry);
        }

        if (info.File is not null)
        {
            File.Delete(info.File);
        }
    }

    public static async Task PerformChanges(Changes changes, CancellationToken ct)
    {
        ImmutableArray<InstalledMod> mods = await Context.Instance.GetMods(ct);

        {
            int nameMaxWidth = 0;
            if (!Console.IsOutputRedirected)
            {
                nameMaxWidth = changes.Install.Aggregate(nameMaxWidth, (a, v) => Math.Max(a, v.Name.Length));
                nameMaxWidth = changes.Uninstall.Aggregate(nameMaxWidth, (a, v) => Math.Max(a, v.Name.Length));
                nameMaxWidth += 2;
                nameMaxWidth = Math.Clamp(nameMaxWidth, 4, Console.WindowWidth * 2 / 3);
            }

            if (!changes.IsEmpty)
            {
                Console.WriteLine();
            }

            foreach (ModInstallInfo update in changes.Install)
            {
                string name = update.Name;

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

                if (Console.IsOutputRedirected)
                {
                    Console.Write(name);
                    Console.Write(' ');
                }
                else
                {
                    if (name.Length > nameMaxWidth)
                    {
                        Console.Write(name.AsSpan(0, nameMaxWidth - 3));
                        Console.Write("...");
                    }
                    else
                    {
                        Console.Write(name);
                    }
                    Console.Write(new string(' ', nameMaxWidth - name.Length));
                }

                IMod? existing = update.LockEntry is null ? null : mods.FirstOrDefault(v => Path.GetFileName(v.FileName) == update.LockEntry.FileName).Mod;

                if (existing is not null)
                {
                    int commonPrefix = 0;
                    for (int i = 0; i < Math.Min(existing.Version.Length, update.Version.VersionNumber.Length); i++)
                    {
                        if (existing.Version[i] != update.Version.VersionNumber[i]) break;
                        commonPrefix = i + 1;
                    }
                    Console.Write('(');
                    Console.Write(existing.Version.AsSpan(0, commonPrefix));
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write(existing.Version.AsSpan(commonPrefix));
                    Console.ResetColor();
                    Console.Write(" -> ");
                    Console.Write(update.Version.VersionNumber.AsSpan(0, commonPrefix));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write(update.Version.VersionNumber.AsSpan(commonPrefix));
                    Console.ResetColor();
                    Console.Write(')');
                }
                else
                {
                    Console.Write($"({update.Version.VersionNumber})");
                }

                if (update.Version.ProjectVersionType == Modrinth.Models.Enums.Project.ProjectVersionType.Alpha)
                {
                    Console.Write(' ');
                    Console.BackgroundColor = ConsoleColor.Yellow;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.Write(" ALPHA ");
                    Console.ResetColor();
                }
                else if (update.Version.ProjectVersionType == Modrinth.Models.Enums.Project.ProjectVersionType.Beta)
                {
                    Console.Write(' ');
                    Console.BackgroundColor = ConsoleColor.Yellow;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.Write(" BETA ");
                    Console.ResetColor();
                }

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($" ({update.Version.Name})");
                Console.ResetColor();

                Console.WriteLine();
            }

            foreach (ModUninstallInfo uninstall in changes.Uninstall)
            {
                string name = uninstall.Name;

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

        if (changes.IsEmpty)
        {
            Log.Info($"No actions have to be done");
        }
        else
        {
            if (Log.AskYesNo("Do you want to perform the actions above?", true))
            {
                await File.WriteAllTextAsync(Context.ModlistPath, JsonSerializer.Serialize(Context.Instance.Modlist, ModListJsonSerializerContext.Default.ModList), ct);

                ProgressBar progressBar = new();

                for (int i = 0; i < changes.Install.Length; i++)
                {
                    ModInstallInfo update = changes.Install[i];

                    progressBar.Report(update.LockEntry?.Name ?? update.Mod.Id, i, changes.Install.Length);

                    await DownloadMod(update, ct);
                }

                progressBar.Dispose();

                for (int i = 0; i < changes.Uninstall.Length; i++)
                {
                    ModUninstallInfo uninstall = changes.Uninstall[i];

                    UninstallMod(uninstall);
                    await File.WriteAllTextAsync(Context.ModlistLockPath, JsonSerializer.Serialize(Context.Instance.ModlistLock, ModLockListJsonSerializerContext.Default.ListModLock), ct);
                }
            }
        }
    }
}
