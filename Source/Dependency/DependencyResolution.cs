using System.Collections.Immutable;
using MMM.Fabric;

namespace MMM;

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
            GenericComponent mod = installedMod.Component;

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

    static bool ShouldSkip(string dependency) => dependency is "fabricloader" or "java" or "another-mod";

    public static async Task<(ImmutableArray<DependencyError> Errors, bool Ok)> CheckDependencies(Context settings, bool printErrorsOnly, CancellationToken ct)
    {
        ImmutableArray<InstalledMod> mods = await settings.GetMods(ct);
        List<InstalledComponent> _components = [
            .. mods.Select(v => new InstalledComponent()
            {
                FileName = v.FileName,
                Component = v.Mod,
            }),
        ];

        _components.Add(new InstalledComponent()
        {
            FileName = null,
            Component = new GenericComponent()
            {
                Name = "Minecraft",
                Id = "minecraft",
                Version = settings.Modlist.GameVersion,
            },
        });

        string? javaVersion = Java.GetVersion();
        if (javaVersion is not null)
        {
            _components.Add(new InstalledComponent()
            {
                FileName = null,
                Component = new GenericComponent()
                {
                    Name = "Java",
                    Id = "java",
                    Version = javaVersion,
                },
            });
        }

        ImmutableArray<InstalledComponent> components = [.. _components];

        ImmutableArray<DependencyError>.Builder errors = ImmutableArray.CreateBuilder<DependencyError>();
        bool ok = true;

        foreach (InstalledMod installedMod in mods)
        {
            FabricMod mod = installedMod.Mod;
            ModLock? lockEntry = settings.ModlistLock.FirstOrDefault(v => v.FileName == Path.GetFileName(installedMod.FileName));
            ModEntry? modEntry = lockEntry is null ? null : settings.Modlist.Mods.FirstOrDefault(v => v.Id == lockEntry.Id);

            if (lockEntry is null) continue;

            if (mod.Depends is not null)
            {
                foreach (KeyValuePair<string, VersionRange> dep in mod.Depends)
                {
                    if (ShouldSkip(dep.Key)) continue;
                    (InstalledComponent? other, DependencyStatus status) = FindBestMatch(dep.Key, dep.Value, components);

                    switch (status)
                    {
                        case DependencyStatus.OK:
                            break;
                        case DependencyStatus.VersionMismatch:
                            ok = false;

                            Log.Error($"Dependency {dep.Key} {dep.Value} for mod {mod.Id} not satisfied (installed version: {other!.Value.Component.Version})");
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
                foreach (KeyValuePair<string, VersionRange> dep in mod.Recommends)
                {
                    if (ShouldSkip(dep.Key)) continue;
                    (InstalledComponent? other, DependencyStatus status) = FindBestMatch(dep.Key, dep.Value, components);

                    switch (status)
                    {
                        case DependencyStatus.OK:
                            break;
                        case DependencyStatus.VersionMismatch:
                            if (!printErrorsOnly) Log.Warning($"Recommendation {dep.Key} {dep.Value} for mod {mod.Id} not satisfied (installed version: {other!.Value.Component.Version})");
                            break;
                        case DependencyStatus.NotFound:
                            if (!printErrorsOnly) Log.Warning($"Recommendation {dep.Key} {dep.Value} for mod {mod.Id} not installed");
                            break;
                    }
                }
            }

            if (mod.Suggests is not null)
            {
                foreach (KeyValuePair<string, VersionRange> dep in mod.Suggests)
                {
                    if (ShouldSkip(dep.Key)) continue;
                    (InstalledComponent? other, DependencyStatus status) = FindBestMatch(dep.Key, dep.Value, components);

                    switch (status)
                    {
                        case DependencyStatus.OK:
                            break;
                        case DependencyStatus.VersionMismatch:
                            if (!printErrorsOnly) Log.Info($"Suggestion {dep.Key} {dep.Value} for mod {mod.Id} not satisfied (installed version: {other!.Value.Component.Version})");
                            break;
                        case DependencyStatus.NotFound:
                            if (!printErrorsOnly) Log.Info($"Suggestion {dep.Key} {dep.Value} for mod {mod.Id} not installed");
                            break;
                    }
                }
            }

            if (mod.Conflicts is not null)
            {
                foreach (KeyValuePair<string, VersionRange> dep in mod.Conflicts)
                {
                    if (ShouldSkip(dep.Key)) continue;
                    (InstalledComponent? other, DependencyStatus status) = FindBestMatch(dep.Key, dep.Value, components);

                    switch (status)
                    {
                        case DependencyStatus.OK:
                            if (!printErrorsOnly) Log.Warning($"Mod {other!.Value.Component.Id} {other.Value.Component.Version} conflicts with {mod.Id}");
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
                foreach (KeyValuePair<string, VersionRange> dep in mod.Breaks)
                {
                    if (ShouldSkip(dep.Key)) continue;
                    (InstalledComponent? other, DependencyStatus status) = FindBestMatch(dep.Key, dep.Value, components);

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

                            Log.Error($"Mod {other!.Value.Component.Id} {other.Value.Component.Version} breaks {mod.Id}");
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
