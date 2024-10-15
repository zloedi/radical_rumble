scp radical_rumble_server.exe radical_rumble_server.pdb ../*.map stoiko@192.168.88.10:/home/stoiko/radical_rumble_server/
ssh stoiko@192.168.88.10 "kill -9 \$(ps -aux | grep radical_rumble_server | awk '{print \$2}')"
ssh stoiko@192.168.88.10 "mv /home/stoiko/radical_rumble_server/radical_rumble_server.log /home/stoiko/radical_rumble_server/radical_rumble_server.log.old"
ssh stoiko@192.168.88.10 "mono --debug /home/stoiko/radical_rumble_server/radical_rumble_server.exe > /home/stoiko/radical_rumble_server/radical_rumble_server.log &"
