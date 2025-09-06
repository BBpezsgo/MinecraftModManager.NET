# Minecraft Mod Manager

[![.Net 10.0](https://img.shields.io/badge/10.0-606060?style=flat-square&logo=dotnet&labelColor=512BD4)](#)

Convinient command line mod manager for Minecraft using Modrinth.

## Features

- Install & uninstall mods
- Update installed mods
- Dependency management
- Conflict check

## Installation

1. Download the binaries from the [Releases](https://github.com/BBpezsgo/MinecraftModManager.NET/releases/latest). (or build it yourself, the script used for publishing the app can be found [here](https://github.com/BBpezsgo/MinecraftModManager.NET/blob/main/publish.sh))
2. You can just use it as it is, no installation required.

> [!NOTE]
> For actually managing Minecraft mods, it requires a package.json that can be created by running the program with no arguments.

> [!TIP]
> Its convinient if you add the binary to the PATH environment variable so you can use it anywhere.

## Usage

`mmm` - Prints the commands you can use.

`mmm update` - Checks for updates and downloads the newer versions.

`mmm add <id>` - Adds the mod with the specified Modrinth id.

`mmm remove <id|name>` - Removes the mod with the specified Modrinth id, display name (ie. Fabric API) or id (ie. fabric-api).

`mmm check` - Checks for dependencies and conflicts, and tries to download the missing mods. And also checks the validity of the lock-file. (untracked/missing files) (I want to implement more features)

`mmm change <version>` - Replaces all the mods with the specified Minecraft version. (unsupported mods will be deleted)

> [!TIP]
> You can put `-h` or `--help` after any command to read about what it does.

> [!TIP]
> You can get the Modrinth id of a mod by going to the mod's page, clicking on the three dots (top right) and clicking "Copy ID".

## Known Issues

- When you use `mmm change`, it doesn't remove the unsupported mods from the listfile. (I mean it's fine, idk if I should fix it or not because it works)

## TODO

- Check for conflicts when adding/removing/updating a mod. (WIP)

All this was heavily inspired by [this project](https://github.com/meza/minecraft-mod-manager/).
