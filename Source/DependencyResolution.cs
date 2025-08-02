using System.Collections.Immutable;
using MMM.Fabric;

namespace MMM;

public enum DependencyErrorLevel
{
    Depends,
    Recommends,
    Suggests,
    Conflicts,
    Breaks,
}

public readonly struct DependencyError
{
    public required DependencyErrorLevel Level { get; init; }
    public required InstalledMod Subject { get; init; }
    public required string OtherId { get; init; }
    public required InstalledComponent? OtherInstalled { get; init; }
}

public static class DependencyResolution
{
    enum DependencyStatus
    {
        OK,
        VersionMismatch,
        NotFound,
    }

    static (InstalledComponent? Mod, DependencyStatus Status) FindBestMatch(string id, VersionRange version, ImmutableArray<InstalledComponent> mods)
    {
        InstalledComponent? best = null;

        foreach (InstalledComponent installedMod in mods)
        {
            var mod = installedMod.Mod;

            if (mod.Id != id)
            {
                if (mod is not FabricMod fabricMod || fabricMod.Provides.IsDefaultOrEmpty || !fabricMod.Provides.Contains(id))
                {
                    continue;
                }
            }

            if (version.Satisfies(mod.Version))
            {
                return (installedMod, DependencyStatus.OK);
            }

            best = installedMod;
        }

        if (best.HasValue)
        {
            return (best, DependencyStatus.VersionMismatch);
        }
        else
        {
            return (null, DependencyStatus.NotFound);
        }
    }

    static bool ShouldSkip(string dependency) =>
        dependency == "fabricloader" ||
        dependency == "java" ||
        dependency == "another-mod";

    public static async Task<(ImmutableArray<DependencyError> Errors, bool Ok)> CheckDependencies(Settings settings, bool printErrorsOnly, CancellationToken ct)
    {
        ImmutableArray<InstalledMod> mods = await settings.ReadMods(ct);
        ImmutableArray<InstalledComponent> components = [
            .. mods.Select(v => new InstalledComponent(v.FileName, v.Mod)),
            new InstalledComponent(null, new GenericComponent(){
                Name = "Minecraft",
                Id = "minecraft",
                Version = settings.Modlist.GameVersion,
            }),
        ];

        ImmutableArray<DependencyError>.Builder errors = ImmutableArray.CreateBuilder<DependencyError>();
        bool ok = true;

        foreach (InstalledMod installedMod in mods)
        {
            FabricMod mod = installedMod.Mod;
            ModlistLockEntry? lockEntry = settings.ModlistLock.FirstOrDefault(v => v.FileName == Path.GetFileName(installedMod.FileName));
            ModlistEntry? modEntry = lockEntry is null ? null : settings.Modlist.Mods.FirstOrDefault(v => v.Id == lockEntry.Id);

            if (lockEntry is null) continue;

            if (mod.Depends is not null)
            {
                foreach (var dep in mod.Depends)
                {
                    if (ShouldSkip(dep.Key)) continue;
                    var (other, status) = FindBestMatch(dep.Key, dep.Value, components);

                    switch (status)
                    {
                        case DependencyStatus.OK:
                            break;
                        case DependencyStatus.VersionMismatch:
                            ok = false;

                            Log.Error($"Dependency {dep.Key} {dep.Value} for mod {mod.Id} not satisfied (installed version: {other!.Value.Mod.Version})");
                            break;
                        case DependencyStatus.NotFound:
                            errors.Add(new DependencyError()
                            {
                                Level = DependencyErrorLevel.Depends,
                                Subject = installedMod,
                                OtherId = dep.Key,
                                OtherInstalled = null,
                            });
                            ok = false;

                            Log.Error($"Dependency {dep.Key} {dep.Value} for mod {mod.Id} not installed");
                            break;
                    }
                }
            }

            if (mod.Recommends is not null)
            {
                foreach (var dep in mod.Recommends)
                {
                    if (ShouldSkip(dep.Key)) continue;
                    var (other, status) = FindBestMatch(dep.Key, dep.Value, components);

                    switch (status)
                    {
                        case DependencyStatus.OK:
                            break;
                        case DependencyStatus.VersionMismatch:
                            if (!printErrorsOnly) Log.Warning($"Recommendation {dep.Key} {dep.Value} for mod {mod.Id} not satisfied (installed version: {other!.Value.Mod.Version})");
                            break;
                        case DependencyStatus.NotFound:
                            if (!printErrorsOnly) Log.Warning($"Recommendation {dep.Key} {dep.Value} for mod {mod.Id} not installed");
                            break;
                    }
                }
            }

            if (mod.Suggests is not null)
            {
                foreach (var dep in mod.Suggests)
                {
                    if (ShouldSkip(dep.Key)) continue;
                    var (other, status) = FindBestMatch(dep.Key, dep.Value, components);

                    switch (status)
                    {
                        case DependencyStatus.OK:
                            break;
                        case DependencyStatus.VersionMismatch:
                            if (!printErrorsOnly) Log.Info($"Suggestion {dep.Key} {dep.Value} for mod {mod.Id} not satisfied (installed version: {other!.Value.Mod.Version})");
                            break;
                        case DependencyStatus.NotFound:
                            if (!printErrorsOnly) Log.Info($"Suggestion {dep.Key} {dep.Value} for mod {mod.Id} not installed");
                            break;
                    }
                }
            }

            if (mod.Conflicts is not null)
            {
                foreach (var dep in mod.Conflicts)
                {
                    if (ShouldSkip(dep.Key)) continue;
                    var (other, status) = FindBestMatch(dep.Key, dep.Value, components);

                    switch (status)
                    {
                        case DependencyStatus.OK:
                            if (!printErrorsOnly) Log.Warning($"Mod {other!.Value.Mod.Id} {other.Value.Mod.Version} conflicts with {mod.Id}");
                            break;
                        case DependencyStatus.VersionMismatch:
                            break;
                        case DependencyStatus.NotFound:
                            break;
                    }
                }
            }

            if (mod.Breaks is not null)
            {
                foreach (var dep in mod.Breaks)
                {
                    if (ShouldSkip(dep.Key)) continue;
                    var (other, status) = FindBestMatch(dep.Key, dep.Value, components);

                    switch (status)
                    {
                        case DependencyStatus.OK:
                            errors.Add(new DependencyError()
                            {
                                Level = DependencyErrorLevel.Breaks,
                                Subject = installedMod,
                                OtherId = dep.Key,
                                OtherInstalled = other,
                            });
                            ok = false;

                            Log.Error($"Mod {other!.Value.Mod.Id} {other.Value.Mod.Version} breaks {mod.Id}");
                            break;
                        case DependencyStatus.VersionMismatch:
                            break;
                        case DependencyStatus.NotFound:
                            break;
                    }
                }
            }
        }

        return (errors.ToImmutable(), ok);
    }
}
