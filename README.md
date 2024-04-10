# Radical Rumble

Radical Rumble is a demo of a casual multiplayer clash/rumble-like game, made specifically for the
**Radical Multiplayer** events at [Prostranstvoto]

[Prostranstvoto]: https://prostranstvoto.bg

## General Build info

The demo can be built both as Unity assembly (game.dll) or a standalone SDL app.
The demo doesn't use any assets besides the ones supplied with the code.
The project is split into these directories:

`BuildSDL/` -- SDL app binaries

`BuildUnity/` -- Unity app binaries

`Code/` -- source code and Visual Studio project files

`RadicalRumbleUnity/` -- Unity project


The ideas behind the 'strange' directory layout:
* allow working outside of Unity
* build the standalone SDL app
* build the headless Standalone Multiplayer Server

The separation of the code from the Unity project allows for code to be kept under separate
repository and/or different version control systems.
Additionaly, the separation allows for the game assembly to be built under Visual Studio and copied
directly to the Unity Build, leading to faster iteration times.

## Building under Unity

1. Open `RadicalRumbleUnity` project with Unity. Any version of Unity should work.
2. Make a build, setting it to point to `BuildUnity/` as target directory.
3. Open the `Code/game.sln` solution in Visual Studio.
4. Build the `game_unity` project. 

The game assembly depends on some Unity assemblies and looks for them at `BuildUnity/RadicalRumble_Data/Managed`
Initially the Unity assemblies are missing, so you need to make an initial build, just so they
appear there.
The Visual Studio project will copy the result assembly and its' matching .pdb to both the Unity
project and the Unity build as a post-build step.

## Building for SDL

1. Open the `Code/game.sln` solution in Visual Studiol
2. Build the `game_sdl` project. 

The Visual Studio project will copy the result exe and its' matching .pdb to `BuildSDL` as a
post-build step.
