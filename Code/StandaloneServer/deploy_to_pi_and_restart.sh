scp wurst_server.exe wurst_server.pdb ../../Build/*.scn stoiko@89.190.193.149:/home/stoiko/wurst_server/
ssh stoiko@89.190.193.149 "kill -9 \$(ps -aux | grep wurst_server | awk '{print \$2}')"
ssh stoiko@89.190.193.149 "mv /home/stoiko/wurst_server/wurst_server.log /home/stoiko/wurst_server/wurst_server.log.old"
ssh stoiko@89.190.193.149 "mono --debug /home/stoiko/wurst_server/wurst_server.exe > /home/stoiko/wurst_server/wurst_server.log &"

