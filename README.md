# Radical Rumble

Radical Rumble is a demo of a casual multiplayer clash/rumble-like game, made specifically for the
**Radical Multiplayer** events at [Prostranstvoto].
It is a Unity3D project, but could also be built as an SDL app without modifications.

[Prostranstvoto]: https://prostranstvoto.bg

## Install
You need to populate the `ZloediUtils` git submodule:

* Cloning with populated `ZloediUtils`:

`git clone --recursive git://github.com/zloedi/radical_rumble.git`
* Alternatively, if you already cloned `radical_rumble.git`, you can get the submodule later:

`git submodule update --init --recursive`

## General Build info
You need some form of Visual Studio/.Net to build the binaries.
If you prefer Unix-like shells, there is a Makefile wrapping the dotnet command.

The project is split into these subdirectories:
* `BuildSDL/` — SDL app binaries
* `BuildUnity/` — Unity app binaries
* `Code/` — source code and Visual Studio project files
* `RadicalRumbleUnity/` — Unity project


The ideas behind the 'esoteric' directory layout:
* allow working outside of Unity
* build the standalone SDL app and Multiplayer Server with Visual Studio (no Unity required)

The separation of the code from the Unity project allows for code to be kept under separate
repository and/or different version control system.
Additionaly, the separation allows for the game assembly to be built under Visual Studio and copied
directly to the Unity Build, leading to faster iteration times.

The demo doesn't use any assets, besides the ones supplied with the code.

## Building under Unity

1. Open `RadicalRumbleUnity` project with Unity. Any version of Unity should work.
2. Make a build, setting it to point to `BuildUnity/` as target directory.
3. Open the `Code/game.sln` solution in Visual Studio.
4. Build the `game_unity` project. 

The game assembly depends on some Unity assemblies and looks for them in `BuildUnity/RadicalRumble_Data/Managed`
Initially the Unity assemblies are missing, so you need to make an initial build, just so they
appear there.
The Visual Studio project will copy the result assembly and its' matching .pdb to both the Unity
project and the Unity build as a post-build step.

## Building for SDL

1. Open the `Code/game.sln` solution in Visual Studio.
2. Build the `game_sdl` project. 

The Visual Studio project will copy the result exe and its' matching .pdb to `BuildSDL` as a
post-build step.
