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
using PDF = Pawn.Def.Flags;

public static class PlayerQGL {

static bool ClSpawnDirectly_kvar = false;

static Player player => Cl.game.player;
static Pawn pawn => Cl.game.pawn;
static Board board => Cl.game.board;
static Game game => Cl.game;

static int _selectedSpawn;
// FIXME: wait for the server clock to match the speculated clock then do spawning
static int _triggerSpawn;

public static void Tick() {
    float mana = 0;
    int myPlayer = 0;
    bool observer = false;
    int myTeam = 0;
    bool needPlayers = game.player.AnyTeamNeedsPlayers();

    int clock = ( int )Cl.clock;
    int clockDelta = ( int )Cl.clockDelta;

    if ( player.IsPlayer( Cl.zport ) ) {
        Cl.TickKeybinds( "play" );
        
        myPlayer = player.GetByZPort( Cl.zport );
        mana = player.Mana( myPlayer, clock );

        Draw.team = myTeam = player.team[myPlayer];
        Draw.mana = mana;
    } else {
        Draw.observer = observer = true;
    }

    pawn.UpdateFilters();
    game.RegisterIntoGrids();

    Pawn.Def selectedDef = Pawn.defs[_selectedSpawn];
    bool allowSpawn = false;

    // FIXME: using the speculated clock here could lead to a no-spawn
    // FIXME: we need to kick the server to send us a clock back
    // FIXME: and postpone a bit before doing any (mana) checks
    bool enoughMana = player.EnoughMana( myPlayer, selectedDef.cost, clock );

    if ( ! needPlayers && enoughMana ) {
        foreach ( var zn in board.filter.zones ) {
            if ( zn.team == myTeam && Draw.IsPointInZone( zn, Cl.mousePosScreen ) ) {
                allowSpawn = true;
                break;
            }
        }
    }

    if ( allowSpawn && _selectedSpawn != 0 && Cl.mouse0Down ) {
        if ( enoughMana ) {
            string name = selectedDef.name;
            string x = Cellophane.FtoA( Cl.mousePosGame.x );
            string y = Cellophane.FtoA( Cl.mousePosGame.y );
            Cl.SvCmd( $"sv_spawn {name} {x} {y}" );
            _selectedSpawn = 0;
        } else {
           Cl.Log( "Can't spawn yet." ); 
        }
    }

    if ( _selectedSpawn != 0 && Cl.mouse1Down ) {
        _selectedSpawn = 0;
    }

    foreach ( var z in pawn.filter.no_garbage ) {
        if ( Cl.TriggerOn( z, Trig.Move ) ) {
            // new movement segment arrives, plan movement on the client
            pawn.mvStart[z] = pawn.mvPos[z];
            pawn.mvStart_ms[z] = clock;
        }
    }

    foreach ( var z in pawn.filter.no_garbage ) {
        int zf = pawn.focus[z];
        if ( Cl.TriggerOn( z, Trig.Attack ) && zf > 0 && pawn.atkEnd_ms[z] > 0 ) {
            // plan attack
            int start = clock;
            int end = pawn.atkEnd_ms[z];
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
    if ( observer ) {
        Draw.Zones( allTeams: true );
    } else if ( _selectedSpawn != 0 ) {
        Draw.Zones();
    }
    Draw.PawnSprites();

    if ( needPlayers ) {
        Draw.centralBigRedMessage = "Waiting for players...";
    } else if ( observer ) {
        if ( ( clock & 512 ) != 0 ) {
            WBUI.QGLTextOutlined( "Observer\n", Draw.wboxScreen, color: Color.white,
                                                                        fontSize: Draw.textSize );
        }
    } else {
        // mana bar
        WrapBox wbox = Draw.wboxScreen.CenterRight( 40 * Draw.pixelSize, Draw.wboxScreen.H );
        WrapBox wbCards = wbox;
        float gap = wbox.W * 0.45f;
        wbox = wbox.Center( gap, wbox.H - gap );
        Color manaCol = new Color( 0.9f, 0.2f, 0.9f );
        Draw.FillRect( wbox.Center( wbox.W + Draw.pixelSize * 2, wbox.H + Draw.pixelSize * 2 ),
                                                                                    manaCol * 0.5f );
        Draw.FillRect( wbox.BottomCenter( wbox.W, wbox.H * mana / 10f ), manaCol );
        WBUI.QGLTextOutlined( ( ( int )mana ).ToString(), wbox, color: manaCol * 4,
                                                        fontSize: Draw.textSize + Draw.pixelSize );

        wbCards = wbCards.BottomRight( 20 * Draw.pixelSize, 20 * Draw.pixelSize,
                                                x: 35 * Draw.pixelSize, y: 20 * Draw.pixelSize );
        foreach ( var def in Pawn.defs ) {
            if ( def.symbol != ' '
                && ( def.flags & ( PDF.Structure
                                | PDF.PatrolWaypoint
                                | PDF.WinObjective ) ) == 0 ) {
                bool enough = player.EnoughMana( myPlayer, def.cost, clock );

                Draw.PawnDef( wbCards.midPoint, def, alpha: enough ? 1 : 0.65f, false );

                WBUI.QGLTextOutlined( $"   {def.cost}",
                        wbCards.CenterRight( wbCards.W / 4, wbCards.H / 4 ),
                        color: enough ? manaCol * 3 : manaCol * 0.7f, fontSize: Draw.textSize );

                wbCards = wbCards.NextUp();
            }
        }
    }

    if ( _selectedSpawn != 0 ) {
        float alpha = allowSpawn ? 1 : 0.45f;
        Draw.PawnDef( Cl.mousePosScreen, _selectedSpawn, alpha: alpha, countDown: ! enoughMana );
    }

    // == end == 

    void snapPos( int z ) {
        pawn.mvPos[z] = pawn.mvEnd[z];
        pawn.mvStart_ms[z] = pawn.mvEnd_ms[z] = clock;
    }

    void updatePos( int z ) {
        // zero delta move means stop
        // FIXME: remove if the pawn state is sent over the network
        if ( pawn.mvEnd_ms[z] <= Cl.serverClock ) {
            // FIXME: should lerp to actual end pos if offshoot
            pawn.mvStart_ms[z] = pawn.mvEnd_ms[z] = clock;
            return;
        }

        // use the unity clock here to keep moving even on crappy synced clock delta
        pawn.SpeculateMovementPosition( z, clock, ( int )( Time.deltaTime * 1000 ) );
    }
}

static void ClSelectToSpawn_kmd( string [] argv ) {
    if ( ClSpawnDirectly_kvar ) {
        Cl.ClForcedSpawn_kmd( argv );
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
