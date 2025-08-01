using System.Collections.Immutable;
using System.Text.Json;
using MMM.Fabric;
using Modrinth;

namespace MMM;

public class Settings
{
    public static readonly string MinecraftPath = "/home/BB/.minecraft";
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
        string minecraftPath = "/home/BB/.minecraft";
        string modlistPath = Path.Combine(minecraftPath, "modlist.json");
        string modlistLockPath = Path.Combine(minecraftPath, "modlist-lock.json");

        List<ModlistLockEntry> modlistLock = [];
        if (File.Exists(modlistLockPath))
        {
            modlistLock = JsonSerializer.Deserialize<List<ModlistLockEntry>>(File.ReadAllText(modlistLockPath)) ?? throw new JsonException();
        }

        Modlist modlist = JsonSerializer.Deserialize<Modlist>(File.ReadAllText(modlistPath)) ?? throw new JsonException();

        return new Settings
        {
            Modlist = modlist,
            ModlistLock = modlistLock,
        };
    }
}

public record struct InstalledMod(string FileName, FabricMod Mod);