using System;

class Game {


public Action<string> Log = s => Qonsole.Log( s );
public Action<string> Error = s => Qonsole.Error( s );

public Shadow shadow = new Shadow();
public Pawn pawn = new Pawn();

// rows sent over the network
public Array [] syncedRows;

public Game() {
    syncedRows = new Array [] {
        pawn.hp,
    };
}

public bool Init( bool skipShadowClones = false ) {
    shadow.Log = Log;
    shadow.Error = Error;
    if ( ! shadow.CreateShadows( pawn, skipClone: skipShadowClones ) ) {
        return false;
    }
    return true;
}

public void Reset() {
    pawn.Reset();
}


}
