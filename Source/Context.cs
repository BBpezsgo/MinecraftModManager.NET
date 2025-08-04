using System.Collections.Immutable;
using System.Text.Json;
using MMM.Fabric;
using Modrinth;

namespace MMM;

public class Context
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

    public required ModList Modlist { get; init; }
    public required List<ModLock> ModlistLock { get; init; }
    public string ModsDirectory => Path.GetFullPath(Path.Combine(MinecraftPath, Modlist.ModsFolder));

    ImmutableArray<InstalledMod> _mods;

    public async Task<ImmutableArray<InstalledMod>> GetMods(CancellationToken ct)
    {
        if (!_mods.IsDefault) return _mods;
        return _mods = await ModReader.ReadMods(ModsDirectory, ct);
    }

    public static Context Create()
    {
        List<ModLock> modlistLock = [];
        if (File.Exists(ModlistLockPath))
        {
            modlistLock = JsonSerializer.Deserialize<List<ModLock>>(File.ReadAllText(ModlistLockPath)) ?? throw new JsonException();
        }

        ModList modlist = JsonSerializer.Deserialize<ModList>(File.ReadAllText(ModlistPath)) ?? throw new JsonException();

        return new Context
        {
            Modlist = modlist,
            ModlistLock = modlistLock,
        };
    }

    public string? GetModName(string? id)
    {
        return id is null ? null : (
            ModlistLock.FirstOrDefault(v => v.Id == id)?.Name
            ?? Modlist.Mods.FirstOrDefault(v => v.Id == id)?.Name
        );
    }

    public async Task<string?> GetModId(string name, CancellationToken ct)
    {
        string? result = ModlistLock.FirstOrDefault(v => v.Id == name || string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))?.Id;
        if (result is not null) return result;

        result = Modlist.Mods.FirstOrDefault(v => v.Id == name || string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))?.Id;
        if (result is not null) return result;

        ImmutableArray<InstalledMod> mods = await GetMods(ct);
        string? filename = Path.GetFileName(mods.FirstOrDefault(v => string.Equals(v.Mod.Id, name, StringComparison.OrdinalIgnoreCase) || string.Equals(v.Mod.Name, name, StringComparison.OrdinalIgnoreCase)).FileName);
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

        string? filename = Path.GetFileName(mods.FirstOrDefault(v => v.Mod.Id == name || v.Mod.Name == name).FileName);
        if (filename is not null)
        {
            return ModlistLock.FirstOrDefault(v => v.FileName == filename)?.Id;
        }

        return null;
    }
}

public readonly struct InstalledComponent
{
    public required string? FileName { get; init; }
    public required GenericComponent Component { get; init; }
}

public readonly struct InstalledMod
{
    public required string FileName { get; init; }
    public required FabricMod Mod { get; init; }
}
