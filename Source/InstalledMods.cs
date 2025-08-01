using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using MMM.Fabric;

namespace MMM;

public static class InstalledMods
{
    static async Task ReadMod(ZipArchive archive, string filename, ImmutableArray<InstalledMod>.Builder result, CancellationToken ct)
    {
        string text;

        {
            var fabricMod = archive.GetEntry("fabric.mod.json") ?? throw new ModLoadException($"File {Path.GetFileName(filename)} is not a fabric mod");
            using Stream stream = await fabricMod.OpenAsync(ct);
            using StreamReader reader = new(stream);
            text = reader.ReadToEnd();
        }

        FabricMod content;
        try
        {
            content = JsonSerializer.Deserialize<FabricMod>(Utils.SanitizeJson(text), FabricModJsonSerializerContext.Default.FabricMod)!;
            result.Add(new InstalledMod(filename, content));
        }
        catch (JsonException e)
        {
            throw new ModLoadException($"Invalid JSON in mod", e);
        }

        if (content.Jars is not null)
        {
            foreach (NestedJarEntry jar in content.Jars)
            {
                ZipArchiveEntry? entry = archive.GetEntry(jar.File);
                string subFilename = Path.Combine(filename, jar.File);

                if (entry is null)
                {
                    Log.Warning($"Nested JAR {subFilename} not found");
                    continue;
                }

                using Stream substream = await entry.OpenAsync(ct);
                using ZipArchive subarchive = new(substream);

                await ReadMod(subarchive, subFilename, result, ct);
            }
        }
    }

    static async Task ReadMod(string file, ImmutableArray<InstalledMod>.Builder result, CancellationToken ct)
    {
        using ZipArchive archive = await ZipFile.OpenReadAsync(file, ct);
        await ReadMod(archive, file, result, ct);
    }

    public static async Task<ImmutableArray<InstalledMod>> ReadMods(string directory, CancellationToken ct)
    {
        ImmutableArray<InstalledMod>.Builder installedMods = ImmutableArray.CreateBuilder<InstalledMod>();

        foreach (string v in Directory.GetFiles(directory, "*.jar"))
        {
            await ReadMod(v, installedMods, ct);
        }

        return installedMods.ToImmutable();
    }

}