using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

using Cl = RRClient;
using model = Draw.model;

public static class PlayerQGL {


static Pawn pawn => Cl.game.pawn;
static Game game => Cl.game;

public static void Tick() {
    pawn.UpdateFilters();
    game.RegisterIntoGrids();

    float dt = Cl.clockDelta / 1000f;

    foreach ( var z in pawn.filter.no_garbage ) {
        Vector2 a = pawn.pos0[z];
        Vector2 b = pawn.pos1[z];
        float d = ( b - a ).magnitude;
        float s = pawn.GetDef( z ).speed / d;
        Draw.model.pos[z] = Vector2.Lerp( a, b, model.t[z] );
        Draw.model.t[z] += s * dt;
    }

    Draw.FillScreen( new Color( 0.1f, 0.13f, 0.2f ) );
    Draw.CenterBoardOnScreen();
    Draw.Board( skipVoidHexes: true );
    Draw.PawnSprites();
}


}
