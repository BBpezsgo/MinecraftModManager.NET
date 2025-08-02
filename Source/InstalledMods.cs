using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using MMM.Fabric;

namespace MMM;

public static class InstalledMods
{
    public static async Task<FabricMod> ReadMod(ZipArchive archive, string filename, CancellationToken ct)
    {
        string text;

        {
            var fabricMod = archive.GetEntry("fabric.mod.json") ?? throw new ModLoadException($"File {Path.GetFileName(filename)} is not a fabric mod");
            using Stream stream = await fabricMod.OpenAsync(ct);
            using StreamReader reader = new(stream);
            text = reader.ReadToEnd();
        }

        try
        {
            return JsonSerializer.Deserialize<FabricMod>(Utils.SanitizeJson(text), FabricModJsonSerializerContext.Default.FabricMod)!;
        }
        catch (JsonException e)
        {
            throw new ModLoadException($"Invalid JSON in mod", e);
        }
    }

    public static async Task<FabricMod> ReadMod(string file, CancellationToken ct)
    {
        try
        {
            using ZipArchive archive = await ZipFile.OpenReadAsync(file, ct);
            return await ReadMod(archive, file, ct);
        }
        catch (InvalidDataException ex)
        {
            throw new ModLoadException($"Invalid mod file", ex);
        }
    }

    static async Task ReadMod(ZipArchive archive, string filename, ImmutableArray<InstalledMod>.Builder result, CancellationToken ct)
    {
        FabricMod content = await ReadMod(archive, filename, ct);
        result.Add(new InstalledMod(filename, content));

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

    static async Task ReadMod(string file, ImmutableArray<InstalledMod>.Builder result, CancellationToken ct)
    {
        try
        {
            using ZipArchive archive = await ZipFile.OpenReadAsync(file, ct);
            await ReadMod(archive, file, result, ct);
        }
        catch (InvalidDataException ex)
        {
            throw new ModLoadException($"Invalid mod file", ex);
        }
    }

    public static async Task<ImmutableArray<InstalledMod>> ReadMods(string directory, CancellationToken ct)
    {
        if (!Directory.Exists(directory)) return [];

        ImmutableArray<InstalledMod>.Builder installedMods = ImmutableArray.CreateBuilder<InstalledMod>();

        foreach (string v in Directory.GetFiles(directory, "*.jar"))
        {
            try
            {
                await ReadMod(v, installedMods, ct);
            }
            catch (ModLoadException ex)
            {
                Log.Error(ex);
            }
        }

        return installedMods.ToImmutable();
    }

}