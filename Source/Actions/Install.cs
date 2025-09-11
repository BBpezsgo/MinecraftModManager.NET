using System.Text.Json;
using Logger;

namespace MMM.Actions;

public static class Install
{
    public static void PerformInstall()
    {
        string loader = "fabric";
        Log.Warning($"The loader will be Fabric. I'll implement more loaders in the future.");
        string? gameVersion = null;
        string? modsFolder = "./mods";

        do
        {
            gameVersion = Log.AskInput("What is the version of Minecraft?", static v =>
            {
                if (!SemanticVersion.TryParse(v, out _))
                {
                    Log.Error($"Invalid version \"{v}\". It must be a semantic version, like 1.21.5");
                    return false;
                }

                return true;
            }, gameVersion);
            modsFolder = Log.AskInput("Where is the mods folder located?", static v =>
            {
                v = Path.GetFullPath(Path.Combine(Context.MinecraftPath, v));

                if (File.Exists(v))
                {
                    Log.Error($"This is a file. ({v})");
                    return false;
                }

                if (Directory.Exists(v) && Directory.EnumerateFileSystemEntries(v).Any())
                {
                    return Log.AskYesNo("The folder isn't empty. Do you want to use it anyway?");
                }

                return true;
            }, modsFolder);

            Console.WriteLine($"Minecraft version: {gameVersion}");
            Console.WriteLine($"Mods folder: {modsFolder}");

            if (Log.AskYesNo("Looks good?", true)) break;
        } while (true);

        File.WriteAllText(Context.ModlistPath, JsonSerializer.Serialize(new ModList()
        {
            Loader = loader,
            GameVersion = gameVersion,
            ModsFolder = modsFolder,
            Mods = [],
        }, ModListJsonSerializerContext.Default.ModList));

        Log.Info($"The package.json was written to {Path.GetFullPath(Context.ModlistPath)}");
    }
}
