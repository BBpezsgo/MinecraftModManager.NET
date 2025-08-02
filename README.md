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

> You can get the Modrinth id of a mod by going to the mod's page, clicking on the three dots (top right) and clicking "Copy ID".

## TODO

- Check for dependencies and conflicts when adding/removing/updating a mod. (WIP)
- Easier Minecraft version change & check. (you can remove the mods folder and the lock file and set the Minecraft version)
- Use the automatic mod search in more places.

All this was heavily inspired by [this project](https://github.com/meza/minecraft-mod-manager/).
