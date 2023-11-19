debug:
	rm ./bin/Debug/game.dll; make ./bin/Debug/game.dll

release:
	rm ./bin/Release/game.dll; make ./bin/Release/game.dll

all: debug release

./bin/Debug/game.dll: #$(OBJS)
	dotnet build --nologo --configuration Debug \
		&& cp ZloediUtils/GLSprites.shader ../RadicalRumbleUnity/Assets/Resources \
		&& cp bin/Debug/game.dll ../RadicalRumbleUnity/Assets/ \
		&& cp bin/Debug/game.pdb ../RadicalRumbleUnity/Assets/ \
		&& cp bin/Debug/game.dll ../Build/RadicalRumble_Data/Managed/ \
		&& cp bin/Debug/game.pdb ../Build/RadicalRumble_Data/Managed/ \

./bin/Release/game.dll: #$(OBJS)
	dotnet build --nologo --configuration Release \
		&& cp ZloediUtils/GLSprites.shader ../RadicalRumbleUnity/Assets/Resources \
		&& cp ./bin/Release/game.dll ../RadicalRumbleUnity/Assets/ \
		&& cp ./bin/Release/game.pdb ../RadicalRumbleUnity/Assets/ \
		&& cp bin/Release/game.dll ../Build/RadicalRumble_Data/Managed/ \
		&& cp bin/Release/game.pdb ../Build/RadicalRumble_Data/Managed/ \

clean: 
	dotnet clean \
	&& rm -rf ./bin/* \
	&& rm -rf ../Vurst_unity/Assets/game.dll* \
	&& rm -rf ../Vurst_unity/Assets/game.pdb* \
