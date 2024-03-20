debug:
	rm ./bin/Debug/radical_rumble_server.exe; make ./bin/Debug/radical_rumble_server.exe

release:
	rm ./bin/Release/radical_rumble_server.exe; make ./bin/Release/radical_rumble_server.exe

all: debug release

./bin/Debug/radical_rumble_server.exe:
	dotnet build --nologo --configuration Debug \


./bin/Release/radical_rumble_server.exe:
	dotnet build --nologo --configuration Release \


clean: 
	dotnet clean \
	&& rm -rf ./bin/* \
	&& rm -rf *.exe \
	&& rm -rf *.pdb \

