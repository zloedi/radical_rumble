rm BuildSDL.zip

#mv /cygdrive/h/My\ Drive/radical_rumble/BuildSDL.zip /cygdrive/h/My\ Drive/radical_rumble/BuildSDL.old.zip

cd ../BuildSDL
zip BuildSDL.zip *.exe *.dll *.map *.pdb -r \
    && mv BuildSDL.zip /cygdrive/h/My\ Drive/radical_rumble/
cd -



cd ../
rm -rf /tmp/radical_rumble_src
git archive --format zip --output /tmp/radical_rumble_src.zip master \
    && mv /tmp/radical_rumble_src.zip /cygdrive/h/My\ Drive/radical_rumble/radical_rumble_src.zip 
cd -

cd ZloediUtils
rm -rf /tmp/ZloediUtils
git archive --format zip --output /tmp/ZloediUtils.zip master \
    && mv /tmp/ZloediUtils.zip /cygdrive/h/My\ Drive/radical_rumble/ZloediUtils.zip 
cd -
