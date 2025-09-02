using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using MMM.Fabric;
using MMM.Forge;
using Tomlyn.Model;

namespace MMM;

public static class ModReader
{
    static async Task<IEnumerable<IMod>?> ReadForgeMod(ZipArchive archive, string filename, CancellationToken ct)
    {
        ZipArchiveEntry? forgeMod = archive.GetEntry("META-INF/mods.toml");
        if (forgeMod is null)
        {
            Log.Warning($"File {Path.GetFileName(filename)} is not a Forge mod");
            return null;
        }

        using Stream stream = await forgeMod.OpenAsync(ct);
        using StreamReader reader = new(stream);
        string text = reader.ReadToEnd();

        TomlTable value = Tomlyn.Toml.ToModel(text, forgeMod.FullName);
        TomlTable? dependencies = value.TryGetValue("dependencies", out object? v) ? (TomlTable)v : null;
        return [.. ((TomlTableArray)value["mods"]).Select(v =>
            {
                return new ForgeMod(v, dependencies);
            })];
    }

    static async Task ReadForgeMod(ZipArchive archive, string filename, ImmutableArray<InstalledMod>.Builder result, CancellationToken ct)
    {
        IEnumerable<IMod>? content = await ReadForgeMod(archive, filename, ct);
        if (content is null) return;

        foreach (IMod item in content)
        {
            result.Add(new InstalledMod()
            {
                FileName = filename,
                Mod = item,
            });
        }
    }

    static async Task<FabricMod?> ReadFabricMod(ZipArchive archive, string filename, CancellationToken ct)
    {
        ZipArchiveEntry? fabricMod = archive.GetEntry("fabric.mod.json");
        if (fabricMod is null)
        {
            Log.Warning($"File {Path.GetFileName(filename)} is not a Fabric mod");
            return null;
        }

        using Stream stream = await fabricMod.OpenAsync(ct);
        using StreamReader reader = new(stream);
        string text = reader.ReadToEnd();

        try
        {
            return JsonSerializer.Deserialize(Utils.SanitizeJson(text), FabricModJsonSerializerContext.Default.FabricMod)!;
        }
        catch (JsonException e)
        {
            throw new ModLoadException($"Invalid JSON in mod", e);
        }
    }

    static async Task ReadFabricMod(ZipArchive archive, string filename, ImmutableArray<InstalledMod>.Builder result, CancellationToken ct)
    {
        FabricMod? content = await ReadFabricMod(archive, filename, ct);
        if (content is null) return;

        for (int i = 0; i < result.Count; i++)
        {
            if (result[i].Mod.Id == content.Id &&
                result[i].Mod.Version == content.Version)
            { goto skipAdd; }
        }

        result.Add(new InstalledMod()
        {
            FileName = filename,
            Mod = content,
        });
    skipAdd:

        if (content.Jars is not null)
        {
            foreach (FabricNestedJarEntry jar in content.Jars)
            {
                ZipArchiveEntry? entry = archive.GetEntry(jar.File);
                string subFilename = Path.Combine(filename, jar.File);

                if (entry is null)
                {
                    throw new ModLoadException($"Nested JAR {subFilename} not found");
                }

                using Stream substream = await entry.OpenAsync(ct);
                using ZipArchive subarchive = new(substream);

                await ReadFabricMod(subarchive, subFilename, result, ct);
            }
        }
    }

    public static async Task<ImmutableArray<InstalledMod>> ReadMods(string directory, CancellationToken ct)
    {
        if (!Directory.Exists(directory)) return [];

        ImmutableArray<InstalledMod>.Builder installedMods = ImmutableArray.CreateBuilder<InstalledMod>();

        foreach (string file in Directory.GetFiles(directory, "*.jar"))
        {
            try
            {
                using ZipArchive archive = await ZipFile.OpenReadAsync(file, ct);
                switch (Context.Instance.Modlist.Loader)
                {
                    case "fabric":
                        await ReadFabricMod(archive, file, installedMods, ct);
                        break;
                    case "forge":
                        await ReadForgeMod(archive, file, installedMods, ct);
                        break;
                }
            }
            catch (InvalidDataException ex)
            {
                Log.Error(new ModLoadException($"Invalid mod file", ex));
            }
        }

        return installedMods.ToImmutable();
    }
}
