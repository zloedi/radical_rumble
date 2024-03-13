using System;
using System.Collections.Generic;
using System.ComponentModel;
#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
using SDLPorts;
#endif

using Cl = RRClient;
using Trig = Pawn.ClientTrigger;

public static class PlayerQGL {

static bool ClSpawnDirectly_kvar = false;

static Player player => Cl.game.player;
static Pawn pawn => Cl.game.pawn;
static Game game => Cl.game;

static int _selectedSpawn;

public static void Tick() {
    if ( player.IsPlayer( Cl.zport ) ) {
        Cl.TickKeybinds( "play" );
        int pl = player.GetByZPort( Cl.zport );
        Draw.team = player.team[pl];
    }

    int clock = ( int )Cl.clock;
    int clockDelta = ( int )Cl.clockDelta;

    pawn.UpdateFilters();
    game.RegisterIntoGrids();

    if ( _selectedSpawn != 0 && Cl.mouse0Down ) {
        string name = Pawn.defs[_selectedSpawn].name;
        string x = Cellophane.FtoA( Cl.mousePosGame.x );
        string y = Cellophane.FtoA( Cl.mousePosGame.y );
        Cl.SvCmd( $"sv_spawn {name} {x} {y}" );
        _selectedSpawn = 0;
    }

    if ( _selectedSpawn != 0 && Cl.mouse1Down ) {
        _selectedSpawn = 0;
    }

    foreach ( var z in pawn.filter.no_garbage ) {
        if ( Cl.TriggerOn( z, Trig.Move ) ) {
            // new movement segment arrives, plan movement on the client
            pawn.mvStart[z] = pawn.mvPos[z];
            pawn.mvStartTime[z] = clock;
        }
    }

    foreach ( var z in pawn.filter.no_garbage ) {
        int zf = pawn.focus[z];
        if ( Cl.TriggerOn( z, Trig.Attack ) && zf > 0 && pawn.atkEndTime[z] > 0 ) {
            // plan attack
            int start = clock;
            int end = pawn.atkEndTime[z];
            int shoot = Mathf.Max( clock, end - ( pawn.AttackTime( z ) - pawn.LoadTime( z ) ) );
            Vector2 a = pawn.mvPos[z];
            Vector2 b = pawn.mvPos[zf];
            SingleShot.AddConditional( dt => {
                int clk = ( int )Cl.clock;

                if ( end <= clk ) {
                    Cl.TriggerRaise( zf, Trig.HurtVisuals );
                    return false;
                }

                if ( pawn.hp[zf] == 0 ) {
                    return false;
                }

                // attacks may be killed off in still loading
                if ( pawn.focus[z] == 0 ) {
                    return false;
                }

                if ( shoot > clk ) {
                    return true;
                }

                if ( ! pawn.IsGarbage( z ) ) {
                    a = pawn.mvPos[z];
                }

                if ( ! pawn.IsGarbage( zf ) ) {
                    b = pawn.mvPos[zf];
                }

                float t = ( clk - shoot ) / ( float )( end - shoot );
                Vector2 c = Vector2.Lerp( a, b, t );

                var ag = Draw.GTS( a );
                var bg = Draw.GTS( b );
                var cg = Draw.GTS( c );

                QGL.LateDrawLine( ag, cg ); 

                return true;
            }, duration: 10 );
        }
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
    if ( _selectedSpawn != 0 ) {
        Draw.PawnDef( Cl.mousePosScreen, _selectedSpawn );
    }

    foreach ( var z in pawn.filter.no_garbage ) {
        if ( pawn.atkEndTime[z] != 0 ) {
            // draw projectile to focus that hits at atkEndTime o clock
        }
    }

    if ( player.IsObserver( Cl.zport ) && ( clock & 512 ) != 0 ) {
        WBUI.QGLTextOutlined( "Observer\n", Draw.wboxScreen, align: 6,
                                                    color: Color.white, fontSize: Draw.textSize );
    }

    // == end == 

    void snapPos( int z ) {
        pawn.mvPos[z] = pawn.mvEnd[z];
        pawn.mvStartTime[z] = pawn.mvEndTime[z] = clock;
    }

    void updatePos( int z ) {
        // zero delta move means stop
        // FIXME: remove if the pawn state is sent over the network
        if ( pawn.mvEndTime[z] <= Cl.serverClock ) {
            // FIXME: should lerp to actual end pos if offshoot
            pawn.mvStartTime[z] = pawn.mvEndTime[z] = clock;
            return;
        }

        // use the unity clock here to keep moving even on crappy synced clock delta
        pawn.SpeculateMovementPosition( z, clock, ( int )( Time.deltaTime * 1000 ) );
    }
}

static void ClSelectToSpawn_kmd( string [] argv ) {
    if ( ClSpawnDirectly_kvar ) {
        Cl.ClSpawn_kmd( argv );
        return;
    }

    if ( argv.Length < 2 ) {
        Cl.Error( $"{argv[0]} <def_name> [team]" );
        return;
    }

    if ( ! Pawn.FindDefIdxByName( argv[1], out int def ) ) {
        Cl.Log( $"{argv[0]} Can't find def named {argv[1]}" );
        return;
    }

    _selectedSpawn = def;
}


}
