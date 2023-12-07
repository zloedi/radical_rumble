debug:
	rm ./bin/Debug/radical_rumble_server.exe; make ./bin/Debug/radical_rumble_server.exe

release:
	rm ./bin/Release/radical_rumble_server.exe; make ./bin/Release/radical_rumble_server.exe

all: debug release

./bin/Debug/radical_rumble_server.exe:
	dotnet build --nologo --configuration Debug \
	&& cp ./bin/Debug/radical_rumble_server.exe ./ \
	&& cp ./bin/Debug/radical_rumble_server.pdb ./ \

./bin/Release/radical_rumble_server.exe:
	dotnet build --nologo --configuration Release \
	&& cp ./bin/Release/radical_rumble_server.exe ./ \
	&& cp ./bin/Release/radical_rumble_server.pdb ./ \

clean: 
	dotnet clean \
	&& rm -rf ./bin/* \

