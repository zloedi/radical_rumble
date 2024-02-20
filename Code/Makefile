debug_unity:
	rm ./bin/Debug/game.dll; make ./bin/Debug/game.dll

debug_sdl:
	rm ./bin/Debug/game_sdl.exe; make ./bin/Debug/game_sdl.exe

release_sdl:
	rm ./bin/Release/game_sdl.exe; make ./bin/Release/game_sdl.exe

release_unity:
	rm ./bin/Release/game.dll; make ./bin/Release/game.dll

all: debug_sdl release_sdl debug_unity release_unity

./bin/Debug/game_sdl.exe:
	dotnet build game_sdl.csproj --nologo --configuration Debug \
		&& cp bin/Debug/game_sdl.exe ../BuildSDL/ \
		&& cp bin/Debug/game_sdl.pdb ../BuildSDL/ \
		&& cp ZloediUtils/SDL2.dll ../BuildSDL \
		&& cp *.map ../BuildSDL \

./bin/Release/game_sdl.exe:
	dotnet build game_sdl.csproj --nologo --configuration Release \
		&& cp bin/Release/game_sdl.exe ../BuildSDL/ \
		&& cp bin/Release/game_sdl.pdb ../BuildSDL/ \
		&& cp ZloediUtils/SDL2.dll ../BuildSDL \
		&& cp *.map ../BuildSDL \

./bin/Debug/game.dll:
	dotnet build game_unity.csproj --nologo --configuration Debug \
		&& cp ZloediUtils/GLSprites.shader ../RadicalRumbleUnity/Assets/Resources \
		&& cp bin/Debug/game.dll ../RadicalRumbleUnity/Assets/ \
		&& cp bin/Debug/game.pdb ../RadicalRumbleUnity/Assets/ \
		&& cp bin/Debug/game.dll ../BuildUnity/RadicalRumble_Data/Managed/ \
		&& cp bin/Debug/game.pdb ../BuildUnity/RadicalRumble_Data/Managed/ \
		&& cp *.map ../BuildUnity \

./bin/Release/game.dll:
	dotnet build game_unity.csproj --nologo --configuration Release \
		&& cp ZloediUtils/GLSprites.shader ../RadicalRumbleUnity/Assets/Resources \
		&& cp ./bin/Release/game.dll ../RadicalRumbleUnity/Assets/ \
		&& cp ./bin/Release/game.pdb ../RadicalRumbleUnity/Assets/ \
		&& cp bin/Release/game.dll ../BuildUnity/RadicalRumble_Data/Managed/ \
		&& cp bin/Release/game.pdb ../BuildUnity/RadicalRumble_Data/Managed/ \
		&& cp *.map ../BuildUnity \

clean: 
	dotnet clean game_sdl.csproj \
	&& rm -rf ./bin/* \
	&& rm -rf ../RadicalRumbleUnity/Assets/game.exe* \
	&& rm -rf ../RadicalRumbleUnity/Assets/game.pdb* \
	&& rm -rf ../RadicalRumbleUnity/Assets/game.dll* \
	&& rm -rf ../RadicalRumbleUnity/Assets/game.pdb* \
