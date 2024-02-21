using System;
using System.Collections.Generic;
using System.ComponentModel;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

using static Pawn.Def;

using Sv = RRServer;

partial class Game {




//bool Gym_AvoidTeammates( int z ) {
//}
//
//bool Gym_FocusOnEnemy( int z ) {
//}

void Gym_TickServer_v2() {

#if false

    pawn.UpdateFilters();
    RegisterIntoGrids();

    foreach ( var z in pawn.filter.ByState( PS.None ) ) {
        pawn.MvClamp( z );
        pawn.SetState( z, PS.Spawning );
    }

    foreach ( var z in pawn.filter.ByState( PS.Spawning ) ) {
        pawn.MvClamp( z );
        if ( Gym_FocusOnEnemy( z ) ) {
            int zFocus = pawn.focus[z];

            // check if there are two simmetrical paths on both sides of a hex
            // and correct start hex by pushing toward the dominating side
            Vector2 snapA = AxialToV( VToAxial( pawn.mvPos[z] ) );
            Vector2 snapB = AxialToV( VToAxial( pawn.mvPos[zFocus] ) );

            float dx = snapA.x - snapB.x;
            if ( dx * dx < 0.0001f ) {
                snapA.x += Mathf.Sign( pawn.mvPos[z].x - snapA.x );
            }
            float dy = snapA.y - snapB.y;
            if ( dy * dy < 0.0001f ) {
                snapA.y += Mathf.Sign( pawn.mvPos[z].y - snapA.y );
            }

            GetCachedPathVec( snapA, pawn.mvEnd[zFocus], out path );
            DebugDrawPath( path );
        }

        pawn.SetState( PS.NavigateToEnemy );
    }

    foreach ( var z in pawn.filter.ByState( PS.Idle ) ) {
        if ( Gym_FocusOnEnemy( z ) ) {
            pawn.SetState( PS.NavigateToEnemy );
        }
    }

    foreach ( var z in pawn.filter.ByState( PS.NavigateToEnemy ) ) {
    }

#endif

}


}
