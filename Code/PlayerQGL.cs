using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

using Cl = RRClient;

public static class PlayerQGL {


static Pawn pawn => Cl.game.pawn;
static Game game => Cl.game;

public static void Tick() {
    int clock = ( int )Cl.clock;
    int clockDelta = ( int )Cl.clockDelta;

    pawn.UpdateFilters();
    game.RegisterIntoGrids();

    void snapPos( int z ) {
        pawn.mvPos[z] = pawn.mvEnd[z];
        pawn.mvStartTime[z] = pawn.mvEndTime[z] = clock;
    }

    void updatePos( int z ) {
        if ( pawn.mvPos[z] == Vector2.zero ) {
            pawn.mvPos[z] = pawn.mvEnd[z];
            pawn.mvStartTime[z] = pawn.mvEndTime[z] = clock;
        }
        // use the unity clock here to keep moving even on crappy synced clock delta
        pawn.SpeculateMovementPosition( z, clock, ( int )( Time.deltaTime * 1000 ) );
    }

    foreach ( var z in pawn.filter.structures ) {
        snapPos( z );
    }

    foreach ( var z in pawn.filter.no_structures ) {
        updatePos( z );
    }

    foreach ( var z in pawn.filter.flying ) {
        updatePos( z );
    }

    Draw.FillScreen( new Color( 0.1f, 0.13f, 0.2f ) );
    Draw.CenterBoardOnScreen();
    Draw.Board( skipVoidHexes: true );
    Draw.PawnSprites();
}


}
