using System;

partial class Game {


public Action<string> Log = s => Qonsole.Log( s );
public Action<string> Error = s => Qonsole.Error( s );

public Shadow shadow = new Shadow();

public Pawn pawn = new Pawn();
public Board board = new Board();

// rows sent over the network
public Array [] syncedRows;
// resizeable board rows 
public Array [] gridRows;

public Game() {
    syncedRows = new Array [] {
        pawn.hp,

        board.size,
        board.terrain,
        board.flags,
    };

    gridRows = new Array [] {
        board.terrain,
        board.flags,
    };
}

public bool Init( bool skipShadowClones = false ) {
    shadow.Log = Log;
    shadow.Error = Error;
    if ( ! shadow.CreateShadows( pawn, skipClone: skipShadowClones ) ) {
        return false;
    }
    if ( ! shadow.CreateShadows( board, maxRow: board.numItems, skipClone: skipShadowClones ) ) {
        return false;
    }
    return true;
}

public void Reset() {
    pawn.Reset();
}


}
