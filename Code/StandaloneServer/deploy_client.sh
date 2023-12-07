rm Buildwurst.zip

mv /cygdrive/j/My\ Drive/wurst/Buildwurst.zip /cygdrive/j/My\ Drive/wurst/Buildwurst.old.zip

cp -rv ../../Build /cygdrive/j/My\ Drive/wurst/

cd ../../Build
zip Buildwurst.zip . -r \
&& mv Buildwurst.zip /cygdrive/j/My\ Drive/wurst/
cd -
