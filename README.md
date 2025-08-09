# Minecraft Mod Manager

[![.Net 10.0](https://img.shields.io/badge/.NET-10.0-5C2D91)](#)

Convinient command line mod manager for Minecraft using Modrinth.

> [!CAUTION]
> Only works with Fabric mods.

## Features

- Install & uninstall mods
- Update installed mods
- Dependency management
- Conflict check

## Usage

`mmm update` - Checks for updates and downloads the newer versions.

`mmm add <id>` - Adds the mod with the specified Modrinth id.

`mmm remove <id|name>` - Removes the mod with the specified Modrinth id, display name (ie. Fabric API) or id (ie. fabric-api).

`mmm check` - Checks for dependencies and conflicts, and tries to download the missing mods. And also checks the validity of the lock-file. (untracked/missing files) (I want to implement more features)

`mmm change <version>` - Replaces all the mods with the specified Minecraft version. (unsupported mods will be deleted)

> You can get the Modrinth id of a mod by going to the mod's page, clicking on the three dots (top right) and clicking "Copy ID".

## Known Issues

- When you use `mmm change`, it doesn't remove the unsupported mods from the listfile. (I mean it's fine, idk if I should fix it or not because it works)

## TODO

- Check for conflicts when adding/removing/updating a mod. (WIP)

All this was heavily inspired by [this project](https://github.com/meza/minecraft-mod-manager/).
