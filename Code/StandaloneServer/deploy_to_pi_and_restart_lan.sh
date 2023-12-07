scp wurst_server.exe wurst_server.pdb stoiko@192.168.1.10:/home/stoiko/wurst_server/
scp ../../Build/*.scn stoiko@192.168.1.10:/home/stoiko/wurst_server/
ssh stoiko@192.168.1.10 "kill -9 \$(ps -aux | grep wurst_server | awk '{print \$2}')"
ssh stoiko@192.168.1.10 "mv /home/stoiko/wurst_server/wurst_server.log /home/stoiko/wurst_server/wurst_server.log.old"
ssh stoiko@192.168.1.10 "mono --debug /home/stoiko/wurst_server/wurst_server.exe > /home/stoiko/wurst_server/wurst_server.log &"
