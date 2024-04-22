using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

namespace RR {
    

using static Pawn.Def;
using Sv = Server;
using PS = Pawn.State;


partial class Game {


const float ATK_MIN_DIST = 0.45f;

#if UNITY_STANDALONE || SDL
[Description( "Show pawn origins on the server." )]
static bool SvShowOrigins_kvar = false;
[Description( "Show pawn radiuses on the server." )]
static bool SvShowRadiuses_kvar = false;
[Description( "Show separation solver debug." )]
static bool SvShowSplits_kvar = false;
//[Description( "Show structure avoidance debug." )]
//static bool SvShowAvoidance_kvar = false;
#endif

[Description( "1 -- Show navigation (patrol) nav lines. 2 -- Show all uncached pather lines on caching." )]
static int SvShowPaths_kvar = 0;
[Description( "Show charge lines." )]
static int SvShowCharge_kvar = 0;
[Description( "Show attack lines." )]
static bool SvShowAttacks_kvar = false;
[Description( "Turns on error checks in the server tick." )]
static bool SvAsserts_kvar = false;
[Description( "Towers don't die." )]
static bool SvInvincibleTowers_kvar = false;
[Description( "Extra logging for the path cache." )]
static bool SvLogPaths_kvar = false;

// 0 -- keep running, 1 -- team0 win, 2 -- team1 win, 3 -- draw
public int TickServer() {

    pawn.UpdateFilters();
    RegisterIntoGrids();

    int gameOver = IsOver();
    if ( gameOver != 0 ) {
        return gameOver;
    }

    foreach ( var z in pawn.filter.ByState( PS.None ) ) {
        Vector2Int p = VToAxial( pawn.mvPos[z] );
        if ( ! board.IsSolid( p ) ) {
            // snap to solid hex if something bad happens
            for ( int y = 0; y < board.height; y++ ) {
                for ( int x = 0; x < board.width; x++ ) {
                    int ix = ( p.x + x ) % board.width;
                    int iy = ( p.y + y ) % board.height;
                    if ( board.IsSolid( ix, iy ) ) {
                        pawn.mvEnd[z] = AxialToV( new Vector2Int( ix, iy ) );
                        Log( $"Snapping to grid {pawn.DN( z )}" );
                        goto quit;
                    }
                }
            }
        }
quit:
        var teammates = pawn.filter.team[pawn.team[z]];
        for ( int i = 0; i < 2; i++ ) {
            foreach ( var zTeammate in teammates ) {
                if ( z == zTeammate ) {
                    continue;
                }
                if ( ! pawn.IsStructure( zTeammate ) ) {
                    continue;
                }
                Vector2 x1 = pawn.mvEnd[z];
                Vector2 x2 = pawn.mvPos[zTeammate];
                float w1 = 1;
                float w2 = 0;
                float r1 = pawn.Radius( z );
                float r2 = pawn.Radius( zTeammate );
                SeparatePoints( x1, x2, w1, w2, r1, r2, out pawn.mvEnd[z], out x2 );
            }
        }
        pawn.mvPos[z] = pawn.mvEnd[z];
        pawn.mvEnd_ms[z] = ZServer.clock;
        pawn.SetState( z, PS.Spawning );
    }

    foreach ( var z in pawn.filter.ByState( PS.Spawning ) ) {
        pawn.MvSnapToEnd( z );
        Log( $"{pawn.DN( z )} is idling." );
        pawn.SetState( z, PS.Idle );
    }

    foreach ( var z in pawn.filter.ByState( PS.Idle ) ) {

        pawn.focus[z] = 0;
        pawn.atkEnd_ms[z] = 0;

        if ( AtkGetFocusPawn( z, out int zEnemy ) ) {

            if ( pawn.IsStructure( z ) ) {
                Log( $"{pawn.DN( z )} starts attacking {pawn.DN( zEnemy )}" );
                pawn.focus[z] = ( byte )zEnemy;
                // a hack to prevent at least turrets firing like crazy on enemy death
                pawn.atkEnd_ms[z] = ZServer.clock + pawn.AttackTime( z );
                pawn.SetState( z, PS.Attack );
                continue;
            }

            MvInterrupt( z );
            charge( z, zEnemy );
            Log( $"{pawn.DN( z )} charges {pawn.DN( zEnemy )}" );
            continue;
        }

        if ( GetPatrolWaypoint( z, out int zPatrol ) ) {
            MvInterrupt( z );
            Log( $"{pawn.DN( z )} patrolling." );
            pawn.SetState( z, PS.Patrol );
        }
    }

    foreach ( var z in pawn.filter.ByState( PS.Patrol ) ) {
        if ( pawn.MvLerp( z, ZServer.clock ) ) {
            // path inflection point, get a new segment to lerp on
            if ( ! MvPatrolUpdate( z ) ) {
                // nothing to focus on for navigation
                Log( $"{pawn.DN( z )} no navigation target, go to idle." );
                pawn.SetState( z, PS.Idle );
                continue;
            }
        }

        if ( AtkGetFocusPawn( z, out int zEnemy ) ) {
            charge( z, zEnemy );
        }
    }

#if false
    foreach ( var z in pawn.filter.ByState( PS.Avoid ) ) {
        if ( pawn.MvLerp( z, ZServer.clock ) ) {
            if ( AtkGetFocusPawn( z, out int zEnemy ) ) {
                charge( z, zEnemy );
            } else {
                MvPatrolUpdate( z );
                pawn.SetState( z, PS.Patrol );
            }
        }
    }
#endif

    foreach ( var z in pawn.filter.ByState( PS.ChargeEnemy ) ) {

        // get nearest attackable enemy to charge
        if ( AtkGetFocusPawn( z, out int ze ) ) {
            pawn.focus[z] = ( byte )ze;
        }

        // nothing to chase, go on patrol
        else {
            Log( $"{pawn.DN( z )} nothing to charge, go to Patrol." );
            pawn.focus[z] = 0;
            MvPatrolUpdate( z );
            pawn.SetState( z, PS.Patrol );
            continue;
        }

        // get the 'feeler' sphere pointing at the enemy
        // if the feeler clips with a teammate currently attacking an enemy,
        //   form a sphere containing the teammate and the enemy
        //   split-solve the feeler against that sphere
        // else
        //   do regular charge

        // we need to lerp before the chase routine changes our end point
        if ( pawn.MvLerp( z, ZServer.clock ) ) {
            // reached attack position, transition to attack
            pawn.MvSnapToEnd( z, ZServer.clock );
            Log( $"{pawn.DN( z )} starts attacking {pawn.DN( pawn.focus[z] )}" );
            pawn.SetState( z, PS.Attack );
            continue;
        }

        // potentially switches the mv lerp end point to something else if the target is moving
        MvChase( z, pawn.focus[z] );
    }

    foreach ( var z in pawn.filter.ByState( PS.Attack ) ) {
        int zEnemy = pawn.focus[z];

        if ( pawn.IsDead( zEnemy ) || pawn.IsGarbage( zEnemy ) ) {
            pawn.focus[z] = 0;
            Log( $"{pawn.DN( z )} idling ({pawn.DN( zEnemy )} out of range)" );
            pawn.SetState( z, PS.Idle );
            continue;
        }

        float r = DistanceForAttack( z, zEnemy ) + 0.5f;
        if ( pawn.SqDist( z, zEnemy ) > r * r ) {
            pawn.atkEnd_ms[z] = 0;
            charge( z, zEnemy );
            continue;
        }

        if ( pawn.atkEnd_ms[z] <= 0 ) {
            pawn.atkEnd_ms[z] = ZServer.clock + pawn.AttackTime( z ) / 2;
        }
    }

    // any ongoing attacks should keep ticking, no matter if in Attack state or not
    foreach ( var z in pawn.filter.no_garbage ) {

        if ( pawn.atkEnd_ms[z] == 0 ) {
            continue;
        }

        if ( pawn.atkEnd_ms[z] >= ZServer.clock ) {
            continue;
        }

        int zf = pawn.focus[z];

        // the focus is already dead and bloated
        if ( pawn.IsDead( zf ) || pawn.IsGarbage( zf ) ) {
            // pawns in the list may die while walking the list
            if ( ! pawn.IsDead( z ) ) {
                Log( $"{pawn.DN( z )} is idling (its focus {pawn.DN( zf )} is dead)." );
                pawn.SetState( z, PS.Idle );
            }
            continue;
        }

        // if still in attack state, loop another attack
        if ( pawn.state[z] == Pawn.SB( PS.Attack ) ) {
            int extra = ZServer.clock - pawn.atkEnd_ms[z];
            pawn.atkEnd_ms[z] = ZServer.clock + pawn.AttackTime( z ) - extra;
        }

        if ( SvShowAttacks_kvar ) {
            DebugLine( pawn.mvPos[z], pawn.mvPos[pawn.focus[z]], duration: 0.25f );
        }

        if ( SvInvincibleTowers_kvar && pawn.IsWinObjective( zf )  ) {
            continue;
        }

        // == damage application ==

        pawn.hp[zf] = ( ushort )Mathf.Max( 0, pawn.hp[zf] - pawn.Damage( z ) );

        if ( pawn.hp[zf] == 0 ) {
            Log( $"{pawn.DN( zf )} is killed." );
            pawn.SetState( zf, PS.Dead );

            // pawns in the list may die while walking the list
            if ( ! pawn.IsDead( z ) ) {
                Log( $"{pawn.DN( z )} is idling (killed its target)." );
                pawn.SetState( z, PS.Idle );
            }
        }
    }

    // remove engagement to dead
    foreach ( var zd in pawn.filter.ByState( PS.Dead ) ) {
        foreach ( var z in pawn.filter.ByState( PS.ChargeEnemy ) ) {
            unfocus( z );
        }

        foreach ( var z in pawn.filter.ByState( PS.Patrol ) ) {
            unfocus( z );
        }

        foreach ( var z in pawn.filter.ByState( PS.Attack ) ) {
            unfocus( z );
        }

        void unfocus( int z ) {
            if ( pawn.focus[z] == zd ) {
                pawn.SetState( z, PS.Idle );
            }
        }
    }

    foreach ( var z in pawn.filter.ByState( PS.Dead ) ) {
        pawn.MvSnapToEnd( z );
        
        // if dead and couldn't reload, kill off this attack
        int t = pawn.AttackTime( z ) - pawn.LoadTime( z );
        if ( pawn.atkEnd_ms[z] - t > ZServer.clock ) {
            Destroy( z );
            continue;
        }

        // the dead pawns attacks may still connect (ranged only?), postpone this a bit
        if ( pawn.atkEnd_ms[z] <= ZServer.clock ) {
            Destroy( z );
        }
    }

#if true // general avoidance

    for ( int team = 0; team < 2; team++ ) {

        // check if an attacker tries to reach a blocked pawn
        // collect attacked but obstructed pawns
        // inflate their sphere to contain to obstructing attacker(s), 
        // allowing blocked attackers to go around
        // later, push this sphere to the general solver and solve only for the blocked attacker

        // moving pawn that cannot reach target
        List<int> avZ = new List<int>();
        // the (defending) target
        List<int> avZDefend = new List<int>();
        // obstruct radius
        List<float> avR = new List<float>();

        var tl = pawn.filter.team[team];
        for ( int iA = 0; iA < tl.Count - 1; iA++ ) {
            for ( int iB = iA + 1; iB < tl.Count; iB++ ) {
                int zA = tl[iA];
                int zB = tl[iB];

                int zAtk = 0;
                int zAvoid = 0;

                if ( pawn.GetState( zA ) == PS.Attack ) {
                    zAtk = zA;
                } else if ( pawn.GetState( zB ) == PS.Attack ) {
                    zAtk = zB;
                }

                if ( pawn.GetState( zA ) != PS.Attack ) {
                    zAvoid = zA;
                } else if ( pawn.GetState( zB ) != PS.Attack ) {
                    zAvoid = zB;
                }

                if ( zAtk == 0 || zAvoid == 0 ) {
                    continue;
                }

                if ( ( pawn.mvPos[zAvoid] - pawn.mvEnd[zAvoid] ).sqrMagnitude < 0.000001f ) {
                    continue;
                }

                int zDef = pawn.focus[zAtk];

                Vector2 dir = ( pawn.mvEnd[zAvoid] - pawn.mvPos[zAvoid] ).normalized;
                float feelerRadius = pawn.Radius( zAvoid );
                Vector2 feeler = pawn.mvPos[zAvoid] + dir * pawn.Radius( zAvoid );

                float dr = pawn.Radius( zAtk ) + feelerRadius;
                if ( ( pawn.mvPos[zAtk] - feeler ).sqrMagnitude >= dr * dr ) {
                    continue;
                }

                avZ.Add( zAvoid );
                avZDefend.Add( zDef );
                Vector2 d = pawn.mvPos[zDef] - pawn.mvPos[zAtk];
                avR.Add( d.magnitude + pawn.Radius( zAtk ) );

                {
                var xx = pawn.mvPos[zDef];
                var rr = avR[avR.Count - 1];
                SingleShot.Add( dt => {
                    Draw.WireCircleGame( xx, rr, Color.cyan );
                }, duration: 0.1f );
                }

                {
                    var xx = feeler;
                    var rr = feelerRadius;
                    SingleShot.Add( dt => {
                        Draw.WireCircleGame( xx, rr, Color.magenta );
                    }, duration: 0.5f );
                }

            }
        }

        List<int> zl = new List<int>();
        List<Vector2> x = new List<Vector2>();
        List<Vector2> xo = new List<Vector2>();
        List<float> w = new List<float>();
        List<float> r = new List<float>();
        List<int> map = new List<int>();

        for ( int i = 0; i < avZ.Count; i++ ) {
            int z = avZDefend[i];

            zl.Add( 0 );
            w.Add( 0 );

            x.Add( pawn.mvPos[z] );
            xo.Add( pawn.mvPos[z] );
            r.Add( avR[i] );

            // remap later
            map.Add( -1 );//avZ[i] );
        }

        foreach ( var z in pawn.filter.team[team] ) {

            var state = pawn.GetState( z );

            if ( state != PS.Patrol && state != PS.Avoid && state != PS.ChargeEnemy ) {
                zl.Add( z );
                x.Add( pawn.mvPos[z] );
                xo.Add( pawn.mvPos[z] );
                w.Add( 0 );
                r.Add( pawn.Radius( z ) );
                map.Add( -1 );
                continue;
            }

            if ( ( pawn.mvPos[z] - pawn.mvEnd[z] ).sqrMagnitude < 0.000001f ) {
                continue;
            }

            Vector2 feeler = pawn.mvPos[z]
                                + ( pawn.mvEnd[z] - pawn.mvPos[z] ).normalized * pawn.Radius( z );

            if ( avZ.Contains( z ) ) {
                float sign = ( z & 1 ) == 0 ? 1f : -1f;
                feeler = pawn.mvPos[z]
                            + Vector2.Perpendicular( ( pawn.mvEnd[z] - pawn.mvPos[z] ).normalized )
                                * sign * pawn.Radius( z );
            }

            zl.Add( z );
            x.Add( feeler );
            xo.Add( feeler );
            w.Add( 1 );
            r.Add( pawn.Radius( z ) );
            map.Add( -1 );
        }

        Gym.SolveOverlapping( x, w, r, map, numSubsteps: 4, overshoot: 0.001f );

        for ( int i = 0; i < zl.Count; i++ ) {
            if ( w[i] == 0 ) {
                continue;
            }

            if ( x[i] == xo[i] ) {
                Qonsole.Log( "no change" );
                continue;
            }

            int z = zl[i];

            Vector2 d = x[i] - pawn.mvPos[z];

            if ( d.sqrMagnitude < 0.05f ) {
                //float sign = ( z & 1 ) == 0 ? 1f : -1f;
                //d = Vector2.Perpendicular( sign * ( pawn.mvEnd[z] - pawn.mvPos[z] ) );
                if ( d.sqrMagnitude < 0.0001f ) {
                    continue;
                }
            }

            pawn.mvEnd[z] = pawn.mvPos[z] + d.normalized * pawn.Radius( z );
            pawn.mvEnd_ms[z] = ZServer.clock + MvDurationMs( z, pawn.mvPos[z], pawn.mvEnd[z] );

            if ( SvShowSplits_kvar ) {
#if UNITY_STANDALONE || SDL
                //Qonsole.Log( ( x[i] - pawn.mvPos[z] ).sqrMagnitude );
                var xx = x[i];//pawn.mvEnd[z];
                var rr = r[i];
                SingleShot.Add( dt => {
                    Draw.WireCircleGame( xx, rr, Color.cyan );
                }, duration: 0.1f );
#endif
            }
        }
    }
#endif

    DebugDrawOrigins();
    DebugDrawRadiuses();

    foreach ( var z in pawn.filter.no_garbage ) {
        pawn.mvEnd_tx[z] = ToTx( pawn.mvEnd[z] );
    }

    if ( SvAsserts_kvar ) {
        pawn.UpdateFilters();

        foreach ( var z in pawn.filter.garbage ) {
            if ( pawn.state[z] != 0 ) {
                Error( $"Garbage pawn {pawn.DN( z )} has state {pawn.GetState( z )}" );
            }
        }

        foreach ( var z in pawn.filter.no_garbage ) {
            if ( pawn.hp[z] == 0 && pawn.state[z] != Pawn.SB( PS.Dead ) ) {
                Error( $"Dead pawn but not in dead state {pawn.DN( z )}" );
            }
        }
    }

    void charge( int z, int zEnemy ) {
        pawn.focus[z] = ( byte )zEnemy;
        // change route segment before next movement lerp
        // so the lerp has proper position
        MvChase( z, zEnemy );
        Log( $"{pawn.DN( z )} charges {pawn.DN( zEnemy )}" );
        pawn.SetState( z, PS.ChargeEnemy );
    }

    return 0;
}

public bool Spawn( int def, float x, float y, out int z ) {
    z = pawn.Create( def );
    if ( z == 0 ) {
        Error( "Out of pawns, can't create." );
        return false;
    }
    pawn.mvPos[z] = pawn.mvEnd[z] = new Vector2( x, y );
    Log( $"Spawned {Pawn.defs[def].name} at idx: {z} pos: {pawn.mvPos[z]}" );
    if ( pawn.IsStructure( z ) ) {
        int hx = VToHex( pawn.mvPos[z] );
        board.pawnDef[hx] = pawn.def[z];
        Log( $"Placing a structure on the grid." );
    }
    return true;
}

public void Destroy( int z ) {
    if ( ( z < 1 && z >= Pawn.MAX_PAWN ) || pawn.IsGarbage( z ) ) {
        Error( $"Invalid pawn {z}" );
        return;
    }
    if ( pawn.IsStructure( z ) ) {
        int hx = VToHex( pawn.mvPos[z] );
        board.pawnDef[hx] = 0;
        Log( $"Removing a structure from the grid." );
    }
    pawn.Destroy( z );
}

public void SetTeam( int z, int team ) {
    pawn.team[z] = ( byte )team;
    if ( pawn.IsStructure( z ) ) {
        int hx = VToHex( pawn.mvPos[z] );
        board.pawnTeam[hx] = pawn.team[z];
        Log( $"Setting team on {z} to {team}." );
    }
}

public void SetZonePoint( int x, int y, int id, int team, int polyIdx = -1 ) {
    if ( x < 0 || x >= board.width || y < 0 || y >= board.height ) {
        return;
    }

    board.UpdateFilters();

    // id zero means remove a zone vertex
    if ( id == 0 ) {
        int hxA = board.Hex( x, y );
        Board.ZoneData zdA = board.UnpackZoneData( board.zone[hxA] );
        foreach ( var hxB in board.filter.solid ) {
            Board.ZoneData zdB = board.UnpackZoneData( board.zone[hxB] );
            if ( zdB.id == zdA.id && zdB.polyIdx > zdA.polyIdx ) {
                zdB.polyIdx--;
                board.zone[hxB] = board.PackZoneData( zdB );
            }
        }
        board.zone[hxA] = 0;
        return;
    }

    board.zone[board.Hex( x, y )] = board.PackZoneData( new Board.ZoneData {
        team = team,
        id = id,
        polyIdx = polyIdx == - 1 ? board.filter.zones[id].polygon.Count : polyIdx,
    } );

    board.UpdateFilters();
}

public void SetTerrain( int x, int y, int terrain ) {
    const int BORDER = 2;

    // nothing to erase outside of the grid
    if ( terrain == 0 && ( x < 0 || x >= board.width || y < 0 || y >= board.height ) ) {
        return;
    }

    if ( x < BORDER || x >= board.width - BORDER
            || y < BORDER || y >= board.height - BORDER ) {
        int minx = Mathf.Min( x - BORDER, 0 );
        int miny = Mathf.Min( y - BORDER, 0 );
        int maxx = Mathf.Max( x + BORDER, board.width - 1 );
        int maxy = Mathf.Max( y + BORDER, board.height - 1 );

        int newW = ( maxx - minx + 1 );
        int newH = ( maxy - miny + 1 );

        if ( newW * newH > Board.MAX_GRID ) {
            // out of space in grid
            return;
        }

        int oldW = board.width;
        int oldH = board.height;

        board.size[0] = ( byte )newW;
        board.size[1] = ( byte )newH;

        foreach ( Array row in gridRows ) {
            Array temp = ( Array )( ( Array )row ).Clone();
            Array.Copy( row, temp, row.Length );
            Array.Clear( row, 0, row.Length );
            for ( int yy = 0; yy < oldH; yy++ ) {
                for ( int xx = 0; xx < oldW; xx++ ) {
                    int dst = xx - minx + ( yy - miny ) * newW;
                    int src = xx + yy * oldW;
                    row.SetValue( temp.GetValue( src ), dst );
                }
            }
            shadow.SetMaxRow( row, board.numItems );
        }

        Log( $"Resized grid. w: {newW} h: {newH}" );
        // FIXME: no server dependencies please!
        Sv.RegisterTrail( $"cl_board_moved {minx} {miny}" );

        x -= minx;
        y -= miny;
    }

    int hx = Mathf.Max( 0, x ) + Mathf.Max( 0, y ) * board.width;

    board.terrain[hx] = ( byte )terrain;

    // trim any zeroes around the valid terrain

    if ( terrain == 0 ) {

        // make sure there is no redundant info on void terrain
        foreach ( Array arr in gridRows ) {
            Array.Clear( arr, hx, 1 );
        }

        int minx = board.width  - 1;
        int miny = board.height - 1;
        int maxx = 0;
        int maxy = 0;

        for ( int yy = 0; yy < board.height; yy++ ) {
            for ( int xx = 0; xx < board.width; xx++ ) {
                int src = xx + yy * board.width;
                foreach ( Array row in gridRows ) {
                    object v = row.GetValue( src );

                    void minmax() {
                        minx = Mathf.Min( minx, Mathf.Max( 0, xx - BORDER ) );
                        maxx = Mathf.Max( maxx, Mathf.Min( board.width - 1, xx + BORDER ) );
                        miny = Mathf.Min( miny, Mathf.Max( 0, yy - BORDER ) );
                        maxy = Mathf.Max( maxy, Mathf.Min( board.height - 1, yy + BORDER ) );
                    }

                    if ( v is byte b && b != 0 ) {
                        minmax(); break;
                    }

                    if ( v is ushort s && s != 0 ) {
                        minmax(); break;
                    }

                    if ( v is int i && i != 0 ) {
                        minmax(); break;
                    }
                }
            }
        }

        int newW = ( maxx - minx + 1 );
        int newH = ( maxy - miny + 1 );

        if ( newW < 0 || newH < 0 ) {
            return;
        }

        int oldW = board.width;
        int oldH = board.height;
        
        if ( newW >= oldW && newH >= oldH ) {
            return;
        }

        board.size[0] = ( byte )newW;
        board.size[1] = ( byte )newH;

        foreach ( Array row in gridRows ) {
            Array temp = ( Array )( ( Array )row ).Clone();
            Array.Copy( row, temp, row.Length );
            Array.Clear( row, 0, row.Length );
            for ( int yy = 0; yy < newH; yy++ ) {
                for ( int xx = 0; xx < newW; xx++ ) {
                    int dst = xx + yy * newW;
                    int src = minx + xx + ( miny + yy ) * oldW;
                    row.SetValue( temp.GetValue( src ), dst );
                }
            }
            shadow.SetMaxRow( row, board.numItems );
        }

        Log( $"Resized grid. w: {newW} h: {newH}" );
        // FIXME: no server dependencies please!
        Sv.RegisterTrail( $"cl_board_moved {minx} {miny}" );
    }
}

static string MapsDir() {
#if UNITY_STANDALONE
    string mapsDevLocation = "/../../Code/";
    string path = Application.dataPath;
    string mapsBuildLocation = Application.isEditor ? "/../../BuildUnity/" : "/../";
    string maps = Directory.Exists( path + mapsDevLocation ) ? mapsDevLocation : mapsBuildLocation;
    return path + maps;
#else
    string mapsDevLocation = "/../Code/";
    string exeLocation = System.Reflection.Assembly.GetEntryAssembly().Location;
    string path = System.IO.Path.GetDirectoryName( exeLocation );
    string maps = Directory.Exists( path + mapsDevLocation ) ? mapsDevLocation : "/./";
    return path + maps;
#endif
}

public void LoadMap( string fileName ) {
    if ( string.IsNullOrEmpty( fileName ) ) {
        return;
    }
    string path = Path.Combine( MapsDir(), fileName );
    string cmd = "";
    try {
        // sv_undelta will do game reset
        cmd = File.ReadAllText( path );
        Reset();
        Cellophane.TryExecuteString( cmd );
        // the slow board filters will be updated conditinionally in 'undelta'
        pawn.UpdateFilters();
        RegisterIntoGrids();
        Log( $"Loaded '{path}'" );
    } catch ( Exception ) {
        Error( $"Failed to read file '{path}'" );
    }
}

public void SaveMap( string fileName ) {
    string path = Path.Combine( MapsDir(), fileName );
    string storage = "sv_undelta\n";
    string emitRow( string name, string c, string v ) {
        return $"{name}{c} :{v} :\n";
    }
    foreach ( Array row in persistentRows ) {
        ArrayShadow.Row shadowRow = shadow.arrayToShadow[row];
        if ( shadowRow.type == ArrayShadow.DeltaType.Uint8 ) {
            byte [] zero = new byte[row.Length];
            if ( Delta.DeltaBytes( ( byte[] )row, zero, out string changes, out string values ) ) {
                storage += emitRow( shadowRow.name, changes, values );
            }
        } else if ( shadowRow.type == ArrayShadow.DeltaType.Uint16 ) {
            ushort [] zero = new ushort[row.Length];
            if ( Delta.DeltaShorts( ( ushort[] )row, zero,
                                                        out string changes, out string values ) ) {
                storage += emitRow( shadowRow.name, changes, values );
            }
        } else if ( shadowRow.type == ArrayShadow.DeltaType.Int32 ) {
            int [] zero = new int[row.Length];
            if ( Delta.DeltaInts( ( int[] )row, zero, out string changes, out string values ) ) {
                storage += emitRow( shadowRow.name, changes, values );
            }
        }
    }
    storage += ";";
    try {
        File.WriteAllText( path, storage );
        Log( $"Saved '{path}'" );
    } catch ( Exception ) {
        Error( $"Failed to save '{path}'" );
    }
}

bool MvCanCrossTerrain( int z, int zTarget ) {
#if false
    if ( zTarget == 0 ) {
        return false;
    }
    Vector2Int axialA = VToAxial( pawn.mvPos[z] );
    Vector2Int axialB = VToAxial( pawn.mvPos[zTarget] );
    return board.CanReach( axialA, axialB );
#else
    return GetCachedPathVec( pawn.mvPos[z], pawn.mvPos[zTarget], out List<int> path ) == 2;
#endif
}

int GetCachedPathEndPos( int zSrc, int zTarget, out List<int> path ) {
    return GetCachedPathVec( pawn.mvEnd[zSrc], pawn.mvEnd[zTarget], out path );
}

int GetCachedPathVec( Vector2 vSrc, Vector2 vTarget, out List<int> path ) {
    return GetCachedPathHex( VToHex( vSrc ), VToHex( vTarget ), out path );
}

// target can be a void hex bordering the solids
Dictionary<int,List<int>> _pathCache = new Dictionary<int,List<int>>();
List<int> _pathError = new List<int>();
int GetCachedPathHex( int hxSrc, int hxTarget, out List<int> path ) {
    if ( hxSrc == 0 ) {
        Error( "Trying to find path from 0" );
        path = _pathError;
        return 0;
    }

    if ( hxTarget == 0 ) {
        Error( "Trying to find path to 0" );
        path = _pathError;
        return 0;
    }

    if ( hxSrc == hxTarget ) {
        path = _pathError;
        return 0;
    }

    int key = ( hxSrc << 16 ) | hxTarget;
    if ( _pathCache.TryGetValue( key, out path ) ) {
        //if ( SvShowPaths_kvar == 2 ) {
        //  DebugDrawPath( path, Color.green );
        //}
        return path.Count;
    }

    log( $"[ffc000]Casting the real pather {hxSrc}->{hxTarget}[-]" );

    int n = 0;
    if ( ! board.GetPath( hxSrc, hxTarget, maxPath: 5 ) ) {
        n += board.patherCTX.diagNumCrossedNodes;
        if ( ! board.GetPath( hxSrc, hxTarget, maxPath: 10 ) ) {
            n += board.patherCTX.diagNumCrossedNodes;
            board.GetPath( hxSrc, hxTarget );
        }
    }
    n += board.patherCTX.diagNumCrossedNodes;

    log( $"[ffc000]Num nodes crossed: {n}[-]" );

    CachePathSubpaths( hxSrc, hxTarget, board.strippedPath );

    path = _pathCache[key];

    if ( SvShowPaths_kvar > 1 ) {
        DebugDrawPath( path, Color.magenta );
    }

    log( $"[ffc000]Path len: {path.Count}[-]" );
    log( $"[ffc000]Num paths in cache: {_pathCache.Count}[-]" );

    void log( string s ) {
        if ( SvLogPaths_kvar ) {
            Log( s );
        }
    }

    return path.Count;
}

void CachePathSubpaths( int hxA, int hxB, List<int> path ) {
    // make sure we use as much as possible of existing paths
    // when navigating i.e. from the opposite side
    for ( int i = 1; i < path.Count; i++ ) {
        int key = ( path[i] << 16 ) | hxB;
        if ( _pathCache.TryGetValue( key, out List<int> tmp ) ) {
            if ( SvLogPaths_kvar ) {
                Log( $"[ffc000]Patching {tmp.Count} nodes out of {path.Count}[-]" );
            }
            path.RemoveRange( i, path.Count - i );
            path.AddRange( tmp );
            break;
        }
    }

    CachePathBothWays( hxA, hxB, path );
    
    // both way mock traversal of the path

    var p = new List<int>( path );
    for ( int i = p.Count - 1; i >= 3; i-- ) {
        p.RemoveAt( i );
        CachePathBothWays( p[0], p[p.Count-1], p );
    }

    p.Clear();
    p.AddRange( path );
    p.Reverse();
    for ( int i = p.Count - 1; i >= 3; i-- ) {
        p.RemoveAt( i );
        CachePathBothWays( p[0], p[p.Count-1], p );
    }
}

void CachePathBothWays( int hxA, int hxB, List<int> path ) {
    if ( hxA == hxB ) {
        Error( "Trying to cache zero path" );
        return;
    }
    
    int key0 = ( hxA << 16 ) | hxB;
    if ( _pathCache.TryGetValue( key0, out List<int> p ) ) {
        return;
    }
    _pathCache[key0] = new List<int>( path );
    //Log( $"[ffc000]Stored {path.Count} nodes at {key0}[-]" );

    int key1 = ( hxB << 16 ) | hxA;
    if ( _pathCache.TryGetValue( key1, out p ) ) {
        return;
    }
    _pathCache[key1] = new List<int>( path );
    _pathCache[key1].Reverse();
    //Log( $"[ffc000]Stored inverted {path.Count} nodes at {key1}[-]" );
}

int MvDurationMs( int z, Vector2 a, Vector2 b ) {
    float segmentDist = ( b - a ).magnitude;
    return ( int )( segmentDist / pawn.SpeedSec( z ) * 1000 );
}

void MvInterrupt( int z ) {
    pawn.MvInterrupt( z, ZServer.clock );
}

// is reachable by land (inert pawns don't care about this one)
bool IsReachableEnemy( int z, int zEnemy ) {
    if ( pawn.IsDead( zEnemy ) || pawn.IsGarbage( zEnemy ) ) {
        return false;
    }

    // non-flying melees can't engage flyers
    if ( ! pawn.IsFlying( z ) && pawn.Range( z ) == 0 && pawn.IsFlying( zEnemy ) ) {
        return false;
    }

    if ( ! MvCanCrossTerrain( z, zEnemy ) ) {
        return false;
    }

    return true;
}

float DistanceForAttack( int zAttacker, int zDefender ) {
    return pawn.Radius( zAttacker )
            + pawn.Radius( zDefender )
            + Mathf.Max( ATK_MIN_DIST, pawn.Range( zAttacker ) );
}

bool AtkGetFocusPawn( int z, out int zEnemy ) {
    zEnemy = 0;
    float minEnemy = 9999999;

    Vector2Int axialA = VToAxial( pawn.mvPos[z] );

    // == structure needs focus ==

    if ( pawn.IsStructure( z ) ) {
        foreach ( var ze in pawn.filter.enemies[pawn.team[z]] ) {
            float sq = pawn.SqDist( z, ze );
            if ( sq >= minEnemy ) {
                continue;
            }

            if ( pawn.IsDead( ze ) || pawn.IsGarbage( ze ) ) {
                continue;
            }

            float r = DistanceForAttack( z, ze );

            // don't care about pawns outside of attack range when structure
            if ( sq > r * r ) {
                continue;
            }

            if ( ! IsReachableEnemy( z, ze ) ) {
                continue;
            }

            zEnemy = ze;
            minEnemy = sq;
        }
        return zEnemy != 0;
    }

    // == non-structure ==

    foreach ( var ze in pawn.filter.enemies[pawn.team[z]] ) {
        float sq = pawn.SqDist( z, ze );

        if ( sq >= minEnemy ) {
            continue;
        }
        
        const float minDist = 6f;
        if ( sq > minDist * minDist ) {
            continue;
        }

        if ( ! IsReachableEnemy( z, ze ) ) {
            continue;
        }
        
        zEnemy = ze;
        minEnemy = sq;
    }

    return zEnemy != 0;
}

Vector2 AtkPointOnEnemy( int z, int zEnemy ) {
    float dist = DistanceForAttack( z, zEnemy );
    Vector2 d = pawn.mvPos[z] - pawn.mvPos[zEnemy];
    float sq = d.sqrMagnitude;
    if ( sq < 0.00001f ) {
        return pawn.mvPos[zEnemy] + Vector2.one * dist;
    }
    if ( sq < dist * dist ) {
        return pawn.mvPos[z];
    }
    return pawn.mvPos[zEnemy] + d / Mathf.Sqrt( sq ) * dist;
}

void MvChase( int z, int zEnemy ) {
    Vector2 chase = AtkPointOnEnemy( z, zEnemy );

    if ( ( pawn.mvEnd[z] - chase ).sqrMagnitude < 0.00001f ) {
        // still chasing the same point
        return;
    }

    // don't change target point too often and spam the network...
    float d = ( chase - pawn.mvPos[z] ).sqrMagnitude;
    if ( ( pawn.mvEnd[z] - chase ).sqrMagnitude < 0.1f * d
        && ( pawn.mvPos[z] - chase ).sqrMagnitude > 1 ) {
        return;
    }

    pawn.mvEnd[z] = chase;
    pawn.mvEnd_ms[z] = ZServer.clock + MvDurationMs( z, pawn.mvPos[z], pawn.mvEnd[z] );

    if ( SvShowCharge_kvar != 0 ) {
        if ( SvShowCharge_kvar == 1 ) {
            DebugSeg( pawn.mvPos[z], pawn.mvEnd[z] );
        } else if ( SvShowCharge_kvar == 2 ) {
            if ( pawn.team[z] == 0 ) {
                DebugSeg( pawn.mvPos[z], pawn.mvEnd[z] );
            }
        } else {
            if ( pawn.team[z] == 1 ) {
                DebugSeg( pawn.mvPos[z], pawn.mvEnd[z] );
            }
        }
    }
}

bool GetPatrolWaypoint( int z, out int zWaypoint ) {
    zWaypoint = 0;
    float minNav = 9999999;

    if ( pawn.IsStructure( z ) ) {
        return false;
    }

    foreach ( var ze in pawn.filter.enemies[pawn.team[z]] ) {
        if ( ! pawn.IsPatrolWaypoint( ze ) ) {
            continue;
        }

        GetCachedPathEndPos( z, ze, out List<int> path );

        float len = 0;
        for ( int i = 0; i < path.Count - 1; i++ ) {
            Vector2 a = HexToV( path[i + 0] );
            Vector2 b = HexToV( path[i + 1] );
            len += ( b - a ).sqrMagnitude;
        }

        if ( len >= minNav ) {
            continue;
        }

        zWaypoint = ze;
        minNav = len;
    }

    //Log( $"Distance to patrol waypoint: {minNav}" );

    return zWaypoint != 0;
}

// movement path inflection point handling
bool MvPatrolUpdate( int z ) {
    List<int> path;
    int zFocus = pawn.focus[z];

    if ( pawn.IsGarbage( zFocus ) ) {
        zFocus = pawn.focus[z] = 0;
    }

    if ( zFocus == 0 ) {
        MvInterrupt( z );

        if ( ! GetPatrolWaypoint( z, out zFocus ) ) {
            return false;
        }

        pawn.focus[z] = ( byte )zFocus;

        if ( GetCachedPathEndPos( z, zFocus, out path ) > 2 ) {
            // push the source position on the side
            // so the path is properly split in the same hex when spawning units

            // FIXME: check if spawned on a hex split by a zone delimiter
            // FIXME: and pick a hex on the proper side

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
        }
    } else {
        GetCachedPathEndPos( z, zFocus, out path );
    }

    if ( path.Count < 2 ) {
        // no path or error
        return false;
    }

    if ( SvShowPaths_kvar == 1 ) {
        DebugDrawPath( path, Color.white );
    }

    // just in case
    pawn.mvPos[z] = pawn.mvEnd[z];

    pawn.mvHex[z] = ( ushort )path[1];

    Vector2 endPos = HexToV( pawn.mvHex[z] );
    pawn.mvEnd[z] = endPos;

#if false
    foreach ( var zOther in pawn.filter.team[pawn.team[z]] ) {
        if ( zOther == z ) {
            continue;
        }

        Vector2 x1 = pawn.mvEnd[z];
        Vector2 x2 = pawn.mvEnd[zOther];

        Vector2 d = x2 - x1;
        float r1 = pawn.Radius( z );
        float r2 = pawn.Radius( zOther );
        float l = d.magnitude;
        float l0 = r1 + r2 + 0.25f;

        if ( l > l0 ) {
            // too far
            continue;
        }

        if ( l < 0.00001f ) {
            Vector2 mp1 = pawn.mvPos[z];
            Vector2 mp2 = pawn.mvPos[zOther];
            x1 += ( mp1 - mp2 ).normalized * 0.01f;
            d = x2 - x1;
            l = d.magnitude;
        }

        if ( SvShowSplits_kvar ) {
#if UNITY_STANDALONE || SDL
            var p = pawn.mvEnd[zOther];
            var r = pawn.Radius( zOther );
            SingleShot.Add( dt => {
                Draw.WireCircleGame( p, r, Color.magenta );
            }, duration: 5 );
#endif
        }

        pawn.mvEnd[z] += ( l - l0 ) * d / l;
    }

    if ( SvShowSplits_kvar && pawn.mvEnd[z] != endPos ) {
#if UNITY_STANDALONE || SDL
        var p = pawn.mvEnd[z];
        var r = pawn.Radius( z );
        SingleShot.Add( dt => {
            Draw.WireCircleGame( p, r, Color.magenta );
        }, duration: 5 );
#endif
    }

#endif

    int leftover = Mathf.Max( 0, ZServer.clock - pawn.mvEnd_ms[z] );
    pawn.mvEnd_ms[z] = ZServer.clock + MvDurationMs( z, pawn.mvPos[z], pawn.mvEnd[z] );

    if ( leftover > 0 ) {
        // advance a bit on the next segment if there is time left from the tick
        if ( ! pawn.MvLerp( z, ZServer.clock + leftover ) ) {
            pawn.mvEnd_ms[z] -= leftover;
        }
    }

    return true;
}

void SeparatePoints( Vector2 x1, Vector2 x2, float w1, float w2, float r1, float r2,
                                                            out Vector2 x1new, out Vector2 x2new ) {
    const float eps = 0.00001f;

    // the actual distance
    float l = ( x2 - x1 ).magnitude;

    if ( l < eps ) {
        float a = ZServer.clock / 1000f;
        x1 += new Vector2( Mathf.Cos( a ), Mathf.Sin( a ) ) * 0.0001f;
        l = ( x2 - x1 ).magnitude;
    }

    // too far, don't bother
    if ( l - ( r1 + r2 ) > 0.001f ) {
        x1new = x1;
        x2new = x2;
        return;
    }

    // desired (rest) distance, make sure we overshoot
    float l0 = r1 + r2 + 0.01f;

    // inverted masses sum
    float sw = w1 + w2;
    if ( sw < eps ) {
        x1new = x1;
        x2new = x2;
        return;
    }

    // solve
    Vector2 s = ( l - l0 ) * ( x2 - x1 ) / l;
    Vector2 dx1 = +w1 / sw * s;
    Vector2 dx2 = -w2 / sw * s;

    x1new = x1 + dx1;
    x2new = x2 + dx2;
}

void DebugDrawPath( List<int> path, Color c ) {
#if UNITY_STANDALONE || SDL
    List<Vector2> pathLine = new List<Vector2>();
    pathLine.Clear();
    foreach ( var hx in path ) {
        pathLine.Add( Draw.HexToScreen( hx ) );
    }
    SingleShot.Add( dt => {
        QGL.LateDrawLine( pathLine, c );
    } );
#endif
}

void DebugSeg( Vector2 a, Vector2 b, float duration = 3, Color? c = null ) {
    DebugLine( a, b, duration, c );
#if UNITY_STANDALONE || SDL
    SingleShot.Add( dt => {
        Hexes.DrawHexWithLines( Draw.GameToScreenPosition( a ),
                                                        Draw.hexPixelSize / 5, Color.white );
        Hexes.DrawHexWithLines( Draw.GameToScreenPosition( b ),
                                                        Draw.hexPixelSize / 5, Color.white );
    }, duration: duration );
#endif
}

void DebugLine( Vector2 a, Vector2 b, float duration = 3, Color? c = null ) {
#if UNITY_STANDALONE || SDL
    Color col = c != null ? c.Value : Color.cyan;
    SingleShot.Add( dt => {
        QGL.LateDrawLine( Draw.GTS( a ), Draw.GTS( b ), color: col );
    }, duration: duration );
#endif
}

void DebugDrawOrigins() {
#if UNITY_STANDALONE || SDL
    if ( ! SvShowOrigins_kvar ) {
        return;
    }
    foreach ( var z in pawn.filter.no_garbage ) {
        SingleShot.Add( dt => {
            Hexes.DrawHexWithLines( Draw.GameToScreenPosition( pawn.mvPos[z] ),
                                                            Draw.hexPixelSize / 4, Color.white );
        }, duration: 1 );
    }
#endif
}

void DebugDrawRadiuses() {
#if UNITY_STANDALONE || SDL
    if ( ! SvShowRadiuses_kvar ) {
        return;
    }
    foreach ( var z in pawn.filter.no_garbage ) {
        SingleShot.Add( dt => {
            Draw.WireCircleGame( pawn.mvPos[z], pawn.Radius( z ), Color.white );
        }, duration: 1 );
    }
#endif
}


} // Game


} // RR
