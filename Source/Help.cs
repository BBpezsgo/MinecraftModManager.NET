static class Help
{
    public static void PrintHelp(string? action)
    {
        switch (action)
        {
            case "update":
                Console.WriteLine($"mmm {action}");
                Console.WriteLine();
                Console.WriteLine($"Checks for updates and downloads the newer versions.");
                break;
            case "add":
                Console.WriteLine($"mmm {action} id <id>");
                Console.WriteLine();
                Console.WriteLine($"Adds the mod with the specified Modrinth id.");
                break;
            case "remove":
                Console.WriteLine($"mmm {action} id <id|name>");
                Console.WriteLine();
                Console.WriteLine($"Removes the mod with the specified Modrinth id, display name (ie. Fabric API) or id (ie. fabric-api).");
                break;
            case "check":
                Console.WriteLine($"mmm {action} id <id>");
                Console.WriteLine();
                Console.WriteLine($"Checks for dependencies and conflicts, and tries to download the missing mods. And also checks the validity of the lock-file. (untracked/missing files)");
                break;
            case "change":
                Console.WriteLine($"mmm {action} id <version>");
                Console.WriteLine();
                Console.WriteLine($"Replaces all the mods with the specified Minecraft version. (unsupported mods will be deleted)");
                break;
            case null:
                Console.WriteLine($"mmm [update|add|remove|check] [-h|--help]");
                break;
        }
    }
}
