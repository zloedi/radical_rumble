debug_sdl:
	rm ./bin/Debug/game_sdl.exe; make ./bin/Debug/game_sdl.exe

release_sdl:
	rm ./bin/Release/game_sdl.exe; make ./bin/Release/game_sdl.exe

debug_unity:
	rm ./bin/Debug/game.dll; make ./bin/Debug/game.dll

release_unity:
	rm ./bin/Release/game.dll; make ./bin/Release/game.dll

all: debug_sdl release_sdl debug_unity release_unity

./bin/Debug/game_sdl.exe:
	dotnet build game_sdl.csproj --nologo --configuration Debug \
		&& cp bin/Debug/game_sdl.exe ../BuildSDL/ \
		&& cp bin/Debug/game_sdl.pdb ../BuildSDL/ \
		&& cp ZloediUtils/SDL/SDL2.dll ../BuildSDL \
		&& cp ZloediUtils/SDL/SDL2.dll bin/Debug/ \

./bin/Release/game_sdl.exe:
	dotnet build game_sdl.csproj --nologo --configuration Release \
		&& cp bin/Release/game_sdl.exe ../BuildSDL/ \
		&& cp bin/Release/game_sdl.pdb ../BuildSDL/ \
		&& cp ZloediUtils/SDL/SDL2.dll ../BuildSDL \
		&& cp ZloediUtils/SDL/SDL2.dll bin/Release/ \

./bin/Debug/game.dll:
	dotnet build game_unity.csproj --nologo --configuration Debug \

./bin/Release/game.dll:
	dotnet build game_unity.csproj --nologo --configuration Release \

clean: 
	dotnet clean game_sdl.csproj \
		&& dotnet clean game_unity.csproj \
		&& rm -rf ./bin/* \
		&& rm -rf ../BuildSDL/game_sdl.exe \
		&& rm -rf ../BuildSDL/game_sdl.exe \
		&& rm -rf ../RadicalRumbleUnity/Assets/game.dll* \
		&& rm -rf ../RadicalRumbleUnity/Assets/game.pdb* \
