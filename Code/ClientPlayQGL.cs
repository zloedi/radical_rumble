using System;
using System.Collections.Generic;
using System.ComponentModel;
#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
using SDLPorts;
#endif

namespace RR {
    

using Cl = RR.Client;
using Trig = RR.Pawn.ClientTrigger;
using PDF = RR.Pawn.Def.Flags;

    
public static class ClientPlayQGL {


static bool ClSpawnDirectly_kvar = false;
static bool ClSkipWaitForPlayers_kvar = false;
static bool ClSkipUI_kvar = false;

static Player player => Cl.game.player;
static Pawn pawn => Cl.game.pawn;
static Board board => Cl.game.board;
static Game game => Cl.game;

static int _selectedSpawn;

public static void Tick() {
    float mana = 0;
    int myPlayer = 0;
    bool observer = false;
    int myTeam = 0;
    bool needPlayers = ! ClSkipWaitForPlayers_kvar && game.player.AnyTeamNeedsPlayers();

    int clock = Cl.clock;

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

    int gameOver = game.IsOver();

    if ( gameOver != 0 ) {
        _selectedSpawn = 0;
    }

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

    // == input ==

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

    // == client logic ==

    foreach ( var z in pawn.filter.no_garbage ) {
        if ( Cl.TrigIsOn( z, Trig.Spawn ) ) {
            pawn.mvPos[z] = pawn.mvEnd[z];
            pawn.mvStart_ms[z] = pawn.mvEnd_ms[z];
            Cl.Log( $"Spawned {pawn.DN( z )}." ); 
        }
    }

    foreach ( var z in pawn.filter.no_garbage ) {
        if ( Cl.TrigIsOn( z, Trig.Move ) ) {
            // new movement segment arrives, plan movement on the client
            pawn.mvStart[z] = pawn.mvPos[z];
            pawn.mvStart_ms[z] = clock - Cl.clockDelta;
            //Cl.Log( $"Plan move for {pawn.DN( z )}." ); 
        }
    }

    // make sure everyone is at the synced position on reconnect
    foreach ( var z in pawn.filter.no_garbage ) {
        if ( Cl.TrigIsOn( z, Trig.Attack ) ) {
            pawn.mvPos[z] = pawn.mvEnd[z];
            pawn.mvStart_ms[z] = pawn.mvEnd_ms[z];
        }
    }

    foreach ( var z in pawn.filter.no_garbage ) {
        int zf = pawn.focus[z];
        if ( Cl.TrigIsOn( z, Trig.Attack ) && zf > 0 && pawn.atkEnd_ms[z] > 0 ) {
            // plan attack
            int start = clock;
            int end = pawn.atkEnd_ms[z];
            int shoot = Mathf.Max( clock, end - ( pawn.AttackTime( z ) - pawn.LoadTime( z ) ) );
            Vector2 a = pawn.mvPos[z];
            Vector2 b = pawn.mvPos[zf];
            SingleShot.AddConditional( dt => {
                int clk = ( int )Cl.clock;

                if ( end <= clk ) {
                    Cl.TrigRaise( zf, Trig.HurtVisuals );
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
        pawn.mvPos[z] = pawn.mvEnd[z];
        pawn.mvStart_ms[z] = pawn.mvEnd_ms[z] = clock;
    }

    foreach ( var z in pawn.filter.no_structures ) {
        // zero delta move means stop
        if ( pawn.mvEnd_ms[z] <= Cl.serverClock && pawn.mvStart_ms[z] != pawn.mvEnd_ms[z] ) {
            // FIXME: should lerp to actual end pos if offshoot
            pawn.mvStart_ms[z] = pawn.mvEnd_ms[z] = clock;
            //Cl.Log( $"{pawn.DN( z )} stops." ); 
            return;
        }

        // the unity clock here is used just to extrapolate (move in the same direction)
        pawn.MvLerpClient( z, clock, Time.deltaTime );
    }

    // == render ==

    Draw.FillScreen( new Color( 0.1f, 0.13f, 0.2f ) );
    Draw.CenterBoardOnScreen();
    Draw.Board( skipVoidHexes: true );
    if ( observer ) {
        Draw.Zones( allTeams: true );
    } else if ( _selectedSpawn != 0 ) {
        Draw.Zones();
    }
    Draw.PawnSprites();

    if ( gameOver != 0 ) {
        if ( gameOver == 3 ) {
            Draw.centralBigRedMessage = "Game Over\n\nDraw!";
        } else {
            Draw.centralBigRedMessage = $"Game Over\n\nPlayer {gameOver} wins!";
        }
    } else if ( needPlayers ) {
        Draw.centralBigRedMessage = "Waiting for players...";
    } else if ( observer ) {
        if ( ( clock & 512 ) != 0 ) {
            WBUI.QGLTextOutlined( "Observer\n", Draw.wboxScreen, color: Color.white,
                                                                        fontSize: Draw.textSize );
        }
    } else if ( ! ClSkipUI_kvar ) {
        // mana bar
        WrapBox wbox = Draw.wboxScreen.CenterRight( 40, Draw.wboxScreen.H );
        WrapBox wbCards = wbox;
        float gap = wbox.W * 0.45f;
        wbox = wbox.Center( gap, wbox.H - gap );
        Color manaCol = new Color( 0.9f, 0.2f, 0.9f );
        Draw.FillRect( wbox.Center( wbox.W + 2, wbox.H + 2 ), manaCol * 0.5f );
        Draw.FillRect( wbox.BottomCenter( wbox.W, wbox.H * mana / 10f ), manaCol );
        WBUI.QGLTextOutlined( ( ( int )mana ).ToString(), wbox, color: manaCol * 4,
                                                        fontSize: Draw.textSize + Draw.pixelSize );

        wbCards = wbCards.BottomRight( 20, 20, x: 35, y: 20 );
        foreach ( var def in Pawn.defs ) {
            if ( def.symbol != ' '
                && ( def.flags & ( PDF.Structure
                                | PDF.PatrolWaypoint
                                | PDF.WinObjective ) ) == 0 ) {
                bool enough = player.EnoughMana( myPlayer, def.cost, clock );

                Draw.PawnDef( wbCards.midPoint, def, alpha: enough ? 1 : 0.65f, false );

                WBUI.QGLTextOutlined( $"  {def.cost}",
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


} }
