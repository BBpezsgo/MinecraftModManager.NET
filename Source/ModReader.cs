using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using MMM.Fabric;

namespace MMM;

public static class ModReader
{
    static async Task<FabricMod> ReadMod(ZipArchive archive, string filename, CancellationToken ct)
    {
        try
        {
            ZipArchiveEntry fabricMod = archive.GetEntry("fabric.mod.json") ?? throw new ModLoadException($"File {Path.GetFileName(filename)} is not a fabric mod");
            using Stream stream = await fabricMod.OpenAsync(ct);
            using StreamReader reader = new(stream);
            string text = reader.ReadToEnd();
            return JsonSerializer.Deserialize(Utils.SanitizeJson(text), FabricModJsonSerializerContext.Default.FabricMod)!;
        }
        catch (JsonException e)
        {
            throw new ModLoadException($"Invalid JSON in mod", e);
        }
    }

    static async Task ReadMod(ZipArchive archive, string filename, ImmutableArray<InstalledMod>.Builder result, CancellationToken ct)
    {
        FabricMod content = await ReadMod(archive, filename, ct);
        result.Add(new InstalledMod()
        {
            FileName = filename,
            Mod = content,
        });

        if (content.Jars is not null)
        {
            foreach (NestedJarEntry jar in content.Jars)
            {
                ZipArchiveEntry? entry = archive.GetEntry(jar.File);
                string subFilename = Path.Combine(filename, jar.File);

                if (entry is null)
                {
                    throw new ModLoadException($"Nested JAR {subFilename} not found");
                }

                using Stream substream = await entry.OpenAsync(ct);
                using ZipArchive subarchive = new(substream);

                await ReadMod(subarchive, subFilename, result, ct);
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
                await ReadMod(archive, file, installedMods, ct);
            }
            catch (InvalidDataException ex)
            {
                Log.Error(new ModLoadException($"Invalid mod file", ex));
            }
        }

        return installedMods.ToImmutable();
    }
}
