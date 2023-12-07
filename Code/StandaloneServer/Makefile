#OBJS=\
#    ../ZloediUtils/Cellophane.cs \
#    ../ZloediUtils/Qonche.cs \
#    ../ZloediUtils/QUI.cs \
#    ../ZloediUtils/Qonsole/Qonsole.cs \
#	../ZloediUtils/Delta.cs \
#    ../ZloediUtils/Hexes.cs \
#    ../ZloediUtils/HexPather.cs \
#    ../ZloediUtils/Net.cs \
#    ../ZloediUtils/GalliumMath/Color.cs \
#    ../ZloediUtils/GalliumMath/Color32.cs \
#    ../ZloediUtils/GalliumMath/Mathf.cs \
#    ../ZloediUtils/GalliumMath/Vector2.cs \
#    ../ZloediUtils/GalliumMath/Vector2Int.cs \
#    ../ZloediUtils/GalliumMath/Vector3.cs \
#    ../ZloediUtils/GalliumMath/Vector3Int.cs \
#	../AbilityCard.cs \
#	../Board.cs \
#	../Common.cs \
#	../Game.cs \
#	../Pawn.cs \
#	../Server.cs \
#    \
#	StandaloneServer.cs \
#    Properties\AssemblyInfo.cs \
#	\
#	../C32_BlurryJab.cs \
#	../C33_TwinStrike.cs \
#	../C34_SapSpeed.cs \
#	../C35_KineticTransfer.cs \
#	../C36_Overdrive.cs \
#	../C37_PowerLeak.cs \
#	../C38_HitandRun.cs \
#	../C39_DriveRecharge.cs \
#	../C40_CascadingReaction.cs \
#	../C41_TemporalDisplacement.cs \
#	../C42_ExperimentalAdjustment.cs \
#	../C43_SandintheHourglass.cs \
#	../C44_BorrowedTime.cs \
#	../C45_SystemsReboot.cs \
#	../C46_RecklessAugmentation.cs \
#	../C47_MakeItCount.cs \
#	../C48_DoubleTime.cs \
#	../C49_PrecisionTiming.cs \
#	../C50_PotentialEnergy.cs \
#	../C51_BreakneckSpeed.cs \
#	../C52_FlashingFlurry.cs \
#	../C53_StabThemAll.cs \
#	../C54_TheKnifesEdge.cs \
#	../C55_PhasingBlades.cs \
#	../C56_RushedtotheEnd.cs \
#	../C57_QuantumUncertainty.cs \
#	../C58_FastestAlive.cs \
#	../C59_ReversetheFlow.cs \
#	../C60_FracturedTimeline.cs \
#	../C61_AirSupport.cs \
#	../C62_TrainedFalcon.cs \

debug:
	rm ./bin/Debug/wurst_server.exe; make ./bin/Debug/wurst_server.exe

release:
	rm ./bin/Release/wurst_server.exe; make ./bin/Release/wurst_server.exe

all: debug release

./bin/Debug/wurst_server.exe: #$(OBJS)
	dotnet build --nologo --configuration Debug \
	&& cp ./bin/Debug/wurst_server.exe ./ \
	&& cp ./bin/Debug/wurst_server.pdb ./ \

./bin/Release/wurst_server.exe: #$(OBJS)
	dotnet build --nologo --configuration Release \
	&& cp ./bin/Release/wurst_server.exe ./ \
	&& cp ./bin/Release/wurst_server.pdb ./ \

clean: 
	dotnet clean \
	&& rm -rf ./bin/* \

