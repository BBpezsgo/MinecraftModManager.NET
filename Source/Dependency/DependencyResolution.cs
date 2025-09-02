using System.Collections.Immutable;
using MMM.Fabric;
using MMM.Forge;

namespace MMM;

public static class DependencyResolution
{
    enum DependencyStatus
    {
        OK,
        VersionMismatch,
        NotFound,
    }

    static (InstalledComponent? Mod, DependencyStatus Status) FindBestMatch(string id, IVersionRange version, ImmutableArray<InstalledComponent> mods)
    {
        InstalledComponent? best = null;

        foreach (InstalledComponent installedMod in mods)
        {
            IMod mod = installedMod.Component;

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

    static bool ShouldSkip(string dependency) => dependency is "fabricloader" or "java" or "another-mod" or "forge" or "neoforge";

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
            IMod mod = installedMod.Mod;
            ModLock? lockEntry = settings.ModlistLock.FirstOrDefault(v => v.FileName == Path.GetFileName(installedMod.FileName));
            ModEntry? modEntry = lockEntry is null ? null : settings.Modlist.Mods.FirstOrDefault(v => v.Id == lockEntry.Id);

            if (lockEntry is null) continue;

            if (mod is FabricMod fabricMod)
            {
                if (fabricMod.Depends is not null)
                {
                    foreach (KeyValuePair<string, FabricVersionRange> dep in fabricMod.Depends)
                    {
                        if (ShouldSkip(dep.Key)) continue;
                        (InstalledComponent? other, DependencyStatus status) = FindBestMatch(dep.Key, dep.Value, components);

                        switch (status)
                        {
                            case DependencyStatus.OK:
                                break;
                            case DependencyStatus.VersionMismatch:
                                ok = false;

                                Log.Error($"Dependency {dep.Key} {dep.Value} for mod {fabricMod.Id} not satisfied (installed version: {other!.Value.Component.Version})");
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

                                Log.Error($"Dependency {dep.Key} {dep.Value} for mod {fabricMod.Id} not installed");
                                break;
                        }
                    }
                }

                if (fabricMod.Recommends is not null)
                {
                    foreach (KeyValuePair<string, FabricVersionRange> dep in fabricMod.Recommends)
                    {
                        if (ShouldSkip(dep.Key)) continue;
                        (InstalledComponent? other, DependencyStatus status) = FindBestMatch(dep.Key, dep.Value, components);

                        switch (status)
                        {
                            case DependencyStatus.OK:
                                break;
                            case DependencyStatus.VersionMismatch:
                                if (!printErrorsOnly) Log.Warning($"Recommendation {dep.Key} {dep.Value} for mod {fabricMod.Id} not satisfied (installed version: {other!.Value.Component.Version})");
                                break;
                            case DependencyStatus.NotFound:
                                if (!printErrorsOnly) Log.Warning($"Recommendation {dep.Key} {dep.Value} for mod {fabricMod.Id} not installed");
                                break;
                        }
                    }
                }

                if (fabricMod.Suggests is not null)
                {
                    foreach (KeyValuePair<string, FabricVersionRange> dep in fabricMod.Suggests)
                    {
                        if (ShouldSkip(dep.Key)) continue;
                        (InstalledComponent? other, DependencyStatus status) = FindBestMatch(dep.Key, dep.Value, components);

                        switch (status)
                        {
                            case DependencyStatus.OK:
                                break;
                            case DependencyStatus.VersionMismatch:
                                if (!printErrorsOnly) Log.Info($"Suggestion {dep.Key} {dep.Value} for mod {fabricMod.Id} not satisfied (installed version: {other!.Value.Component.Version})");
                                break;
                            case DependencyStatus.NotFound:
                                if (!printErrorsOnly) Log.Info($"Suggestion {dep.Key} {dep.Value} for mod {fabricMod.Id} not installed");
                                break;
                        }
                    }
                }

                if (fabricMod.Conflicts is not null)
                {
                    foreach (KeyValuePair<string, FabricVersionRange> dep in fabricMod.Conflicts)
                    {
                        if (ShouldSkip(dep.Key)) continue;
                        (InstalledComponent? other, DependencyStatus status) = FindBestMatch(dep.Key, dep.Value, components);

                        switch (status)
                        {
                            case DependencyStatus.OK:
                                if (!printErrorsOnly) Log.Warning($"Mod {other!.Value.Component.Id} {other.Value.Component.Version} conflicts with {fabricMod.Id}");
                                break;
                            case DependencyStatus.VersionMismatch:
                                break;
                            case DependencyStatus.NotFound:
                                break;
                        }
                    }
                }

                if (fabricMod.Breaks is not null)
                {
                    foreach (KeyValuePair<string, FabricVersionRange> dep in fabricMod.Breaks)
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

                                Log.Error($"Mod {other!.Value.Component.Id} {other.Value.Component.Version} breaks {fabricMod.Id}");
                                break;
                            case DependencyStatus.VersionMismatch:
                                break;
                            case DependencyStatus.NotFound:
                                break;
                        }
                    }
                }
            }
            else if (mod is ForgeMod forgeMod)
            {
                foreach (ForgeModDependency dep in forgeMod.Dependencies)
                {
                    if (ShouldSkip(dep.ModId)) continue;
                    (InstalledComponent? other, DependencyStatus status) = FindBestMatch(dep.ModId, dep.VersionRange, components);

                    if (dep.Mandatory)
                    {
                        switch (status)
                        {
                            case DependencyStatus.OK:
                                break;
                            case DependencyStatus.VersionMismatch:
                                ok = false;

                                Log.Error($"Dependency {dep.ModId} {dep.VersionRange} for mod {forgeMod.Id} not satisfied (installed version: {other!.Value.Component.Version})");
                                break;
                            case DependencyStatus.NotFound:
                                errors.Add(new DependencyError()
                                {
                                    Level = DependencyErrorLevel.Depends,
                                    Subject = installedMod,
                                    OtherId = dep.ModId,
                                    OtherInstalled = null,
                                });
                                ok = false;

                                Log.Error($"Dependency {dep.ModId} {dep.VersionRange} for mod {forgeMod.Id} not installed");
                                break;
                        }
                    }
                    else
                    {
                        switch (status)
                        {
                            case DependencyStatus.OK:
                                break;
                            case DependencyStatus.VersionMismatch:
                                if (!printErrorsOnly) Log.Warning($"Recommendation {dep.ModId} {dep.VersionRange} for mod {forgeMod.Id} not satisfied (installed version: {other!.Value.Component.Version})");
                                break;
                            case DependencyStatus.NotFound:
                                if (!printErrorsOnly) Log.Warning($"Recommendation {dep.ModId} {dep.VersionRange} for mod {forgeMod.Id} not installed");
                                break;
                        }
                    }
                }
            }
        }

        return (errors.ToImmutable(), ok);
    }
}
