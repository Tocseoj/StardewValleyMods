# Tocseoj's Stardew Valley mods

[![Nexus Mods](https://img.shields.io/badge/Nexus-Mods-4DB7FF.svg)](https://www.nexusmods.com/users/165805258?tab=user+files)
[![ModDrop](https://img.shields.io/badge/ModDrop-ModDrop-4DB7FF.svg)](https://www.moddrop.com/stardew-valley/mods/1549539-ladder-lightj)
[![GitHub](https://img.shields.io/badge/GitHub-Tocseoj-4DB7FF.svg)](https://github.com/Tocseoj)

This repository contains my SMAPI mods for Stardew Valley.

## Mods

Active mods:

- **Ladder Light** <small>([ModDrop](https://www.moddrop.com/stardew-valley/mods/1549539-ladder-light) | Nexus | [source](LadderLight))</small>
  _Makes mine ladders and shafts have a slight glow to them, so you don't lose them on levels 30-40._

## Compiling the mods

Installing stable releases from Nexus Mods is recommended for most users. If you really want to
compile the mod yourself, read on.

These mods use the [crossplatform build config](https://www.nuget.org/packages/Pathoschild.Stardew.ModBuildConfig)
so they can be built on Linux, Mac, and Windows without changes. See [the build config documentation](https://www.nuget.org/packages/Pathoschild.Stardew.ModBuildConfig)
for troubleshooting.

### Compiling a mod for testing

To compile a mod and add it to your game's `Mods` directory:

1. Rebuild the project in [Visual Studio](https://www.visualstudio.com/vs/community/) or [MonoDevelop](https://www.monodevelop.com/).
   <small>This will compile the code and package it into the mod directory.</small>
2. Launch the project with debugging.
   <small>This will start the game through SMAPI and attach the Visual Studio debugger.</small>

### Compiling a mod for release

To package a mod for release:

1. Switch to `Release` build configuration.
2. Recompile the mod per the previous section.
3. Upload the generated `bin/Release/<mod name>-<version>.zip` file from the project folder.
