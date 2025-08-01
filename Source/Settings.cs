using System.Collections.Immutable;
using System.Text.Json;
using MMM.Fabric;
using Modrinth;

namespace MMM;

public class Settings
{
    public static readonly string MinecraftPath =
#if DEBUG
    $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.minecraft/";
#else
    Environment.CurrentDirectory;
#endif

    public static readonly string ModlistPath = Path.Combine(MinecraftPath, "modlist.json");
    public static readonly string ModlistLockPath = Path.Combine(MinecraftPath, "modlist-lock.json");

    public static readonly ModrinthClientConfig ModrinthClientConfig = new()
    {
        UserAgent = Project.UserAgent,
    };

    public required Modlist Modlist { get; init; }
    public required List<ModlistLockEntry> ModlistLock { get; init; }
    public string ModsDirectory => Path.GetFullPath(Path.Combine(MinecraftPath, Modlist.ModsFolder));

    ImmutableArray<InstalledMod> _mods;

    public async Task<ImmutableArray<InstalledMod>> ReadMods(CancellationToken ct)
    {
        if (!_mods.IsDefault) return _mods;
        return _mods = await InstalledMods.ReadMods(ModsDirectory, ct);
    }

    public static Settings Create()
    {
        List<ModlistLockEntry> modlistLock = [];
        if (File.Exists(ModlistLockPath))
        {
            modlistLock = JsonSerializer.Deserialize<List<ModlistLockEntry>>(File.ReadAllText(ModlistLockPath)) ?? throw new JsonException();
        }

        Modlist modlist = JsonSerializer.Deserialize<Modlist>(File.ReadAllText(ModlistPath)) ?? throw new JsonException();

        return new Settings
        {
            Modlist = modlist,
            ModlistLock = modlistLock,
        };
    }

    public string? GetModName(string? id) =>
        id is null ? null : (
            ModlistLock.FirstOrDefault(v => v.Id == id)?.Name
            ?? Modlist.Mods.FirstOrDefault(v => v.Id == id)?.Name
        );

    public async Task<string?> GetModId(string name, CancellationToken ct)
    {
        string? result = ModlistLock.FirstOrDefault(v => v.Id == name || string.Equals(v.Name, name, StringComparison.InvariantCultureIgnoreCase))?.Id;
        if (result is not null) return result;

        result = Modlist.Mods.FirstOrDefault(v => v.Id == name || string.Equals(v.Name, name, StringComparison.InvariantCultureIgnoreCase))?.Id;
        if (result is not null) return result;

        var mods = await ReadMods(ct);
        var filename = Path.GetFileName(mods.FirstOrDefault(v => string.Equals(v.Mod.Id, name, StringComparison.InvariantCultureIgnoreCase) || string.Equals(v.Mod.Name, name, StringComparison.InvariantCultureIgnoreCase)).FileName);
        if (filename is not null)
        {
            return ModlistLock.FirstOrDefault(v => v.FileName == filename)?.Id;
        }

        return null;
    }

    public string? GetModId(string name, ImmutableArray<InstalledMod> mods)
    {
        string? result = ModlistLock.FirstOrDefault(v => v.Id == name || v.Name == name)?.Id;
        if (result is not null) return result;

        result = Modlist.Mods.FirstOrDefault(v => v.Id == name || v.Name == name)?.Id;
        if (result is not null) return result;

        var filename = Path.GetFileName(mods.FirstOrDefault(v => v.Mod.Id == name || v.Mod.Name == name).FileName);
        if (filename is not null)
        {
            return ModlistLock.FirstOrDefault(v => v.FileName == filename)?.Id;
        }

        return null;
    }
}

public record struct InstalledComponent(string? FileName, GenericComponent Mod);

public record struct InstalledMod(string FileName, FabricMod Mod);
