scp radical_rumble_server.exe radical_rumble_server.pdb ../*.map stoiko@89.190.193.149:/home/stoiko/radical_rumble_server/
ssh stoiko@89.190.193.149 "kill -9 \$(ps -aux | grep radical_rumble_server | awk '{print \$2}')"
ssh stoiko@89.190.193.149 "mv /home/stoiko/radical_rumble_server/radical_rumble_server.log /home/stoiko/radical_rumble_server/radical_rumble_server.log.old"
ssh stoiko@89.190.193.149 "mono --debug /home/stoiko/radical_rumble_server/radical_rumble_server.exe > /home/stoiko/radical_rumble_server/radical_rumble_server.log &"
