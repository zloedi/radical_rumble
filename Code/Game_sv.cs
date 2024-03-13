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
using PS = Pawn.State;

partial class Game {

const float ATK_MIN_DIST = 0.45f;

#if UNITY_STANDALONE || SDL
[Description( "1 -- Show navigation (patrol) nav lines. 2 -- Show all uncached pather lines on caching." )]
static int SvShowPaths_kvar = 0;
[Description( "Show attack lines." )]
static bool SvShowAttacks_kvar = false;
[Description( "Show pawn origins on the server." )]
static bool SvShowOrigins_kvar = false;
//[Description( "Show structure avoidance debug." )]
//static bool SvShowAvoidance_kvar = false;
[Description( "Show charge lines." )]
static int SvShowCharge_kvar = 0;
#endif

[Description( "Turns on error checks in the server tick." )]
static bool SvAsserts_kvar = false;
[Description( "Towers don't die." )]
static bool SvInvincibleTowers_kvar = false;
[Description( "Extra logging for the path cache." )]
static bool SvLogPaths_kvar = false;
//static int ChickenBit_kvar = 0;

public void PostLoadMap() {
    pawn.UpdateFilters();
}

public void TickServer() {
    pawn.UpdateFilters();
    RegisterIntoGrids();

    foreach ( var z in pawn.filter.ByState( PS.None ) ) {
        pawn.MvClamp( z );
        Vector2Int p = VToAxial( pawn.mvPos[z] );
        if ( ! board.IsSolid( p ) ) {
            // snap to solid hex if something bad happens
            for ( int y = 0; y < board.height; y++ ) {
                for ( int x = 0; x < board.width; x++ ) {
                    int ix = ( p.x + x ) % board.width;
                    int iy = ( p.y + y ) % board.height;
                    if ( board.IsSolid( ix, iy ) ) {
                        pawn.mvEnd[z] = AxialToV( new Vector2Int( ix, iy ) );
                        pawn.MvClamp( z );
                        Log( $"Snapping to grid {pawn.DN( z )}" );
                        goto quit;
                    }
                }
            }
        }
quit:
        pawn.SetState( z, PS.Spawning );
    }

    foreach ( var z in pawn.filter.ByState( PS.Spawning ) ) {
        pawn.MvClamp( z );
        Log( $"{pawn.DN( z )} is idling." );
        pawn.SetState( z, PS.Idle );
    }

    foreach ( var z in pawn.filter.ByState( PS.Idle ) ) {

        pawn.focus[z] = 0;
        pawn.atkEndTime[z] = 0;

        if ( AtkGetFocusPawn( z, out int zEnemy ) ) {

            if ( pawn.IsStructure( z ) ) {
                Log( $"{pawn.DN( z )} starts attacking {pawn.DN( zEnemy )}" );
                pawn.focus[z] = ( byte )zEnemy;
                // a hack to prevent at least turrets firing like crazy on enemy death
                pawn.atkEndTime[z] = ZServer.clock + pawn.AttackTime( z );
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
            if ( ! NavUpdate( z ) ) {
                // nothing to focus on for navigation
                pawn.SetState( z, PS.Idle );
                continue;
            }
        }

        if ( AtkGetFocusPawn( z, out int zEnemy ) ) {
            charge( z, zEnemy );
            continue;
        }
    }

    foreach ( var z in pawn.filter.ByState( PS.ChargeEnemy ) ) {

        // get nearest attackable enemy to charge
        if ( AtkGetFocusPawn( z, out int ze ) ) {
            pawn.focus[z] = ( byte )ze;
        }

        // nothing to chase, go on patrol
        else {
            pawn.focus[z] = 0;
            NavUpdate( z );
            Log( $"{pawn.DN( z )} nothing to charge, go to Patrol." );
            pawn.SetState( z, PS.Patrol );
            continue;
        }

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
            pawn.atkEndTime[z] = 0;
            charge( z, zEnemy );
            continue;
        }

        if ( pawn.atkEndTime[z] <= 0 ) {
            pawn.atkEndTime[z] = ZServer.clock + pawn.AttackTime( z ) / 2;
        }
    }

    // any ongoing attacks should keep ticking, no matter if in Attack state or not
    foreach ( var z in pawn.filter.no_garbage ) {

        if ( pawn.atkEndTime[z] == 0 ) {
            continue;
        }

        if ( pawn.atkEndTime[z] >= ZServer.clock ) {
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
            int extra = ZServer.clock - pawn.atkEndTime[z];
            pawn.atkEndTime[z] = ZServer.clock + pawn.AttackTime( z ) - extra;
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
            Log( $"{pawn.DN( z )} is killed." );
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
        pawn.MvClamp( z );
        
        // if dead and couldn't reload, kill off this attack
        int t = pawn.AttackTime( z ) - pawn.LoadTime( z );
        if ( pawn.atkEndTime[z] - t > ZServer.clock ) {
            Destroy( z );
            continue;
        }

        // the dead pawns attacks may still connect (ranged only?), postpone this a bit
        if ( pawn.atkEndTime[z] <= ZServer.clock ) {
            Destroy( z );
        }
    }

    DebugDrawOrigins();

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
}

public bool Spawn( int def, float x, float y, out int z ) {
    z = pawn.Create( def );
    if ( z == 0 ) {
        Error( "Out of pawns, can't create." );
        return false;
    }
    pawn.mvPos[z] = pawn.mvStart[z] = pawn.mvEnd[z] = new Vector2( x, y );
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

public void AddZonePoint( int x, int y, int team, int id ) {
    if ( x < 0 || x >= board.width || y < 0 || y >= board.height ) {
        return;
    }

    board.UpdateFilters();

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
        polyIdx = board.filter.zones[id].polygon.Count,
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
        Sv.RegisterTrail( $"cl_board_moved {minx} {miny}" );
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
    if ( hxSrc == 0 || hxTarget == 0 ) {
        Error( "Trying to find path to/from 0" );
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

int MvDuration( int z ) {
    float segmentDist = ( pawn.mvEnd[z] - pawn.mvStart[z] ).magnitude;
    return ( 60 * ToTx( segmentDist ) / pawn.Speed( z ) * 1000 ) >> FRAC_BITS;
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

    //Vector2 atk = AtkPointOnEnemy( z, zEnemy );
    //if ( AvoidStructure( pawn.team[z], pawn.mvPos[z], atk, out Vector2 asp ) ) {
    //    return false;
    //}

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

    pawn.mvStart[z] = pawn.mvPos[z];
    pawn.mvEnd[z] = chase;
    pawn.mvStartTime[z] = ZServer.clock;
    pawn.mvEndTime[z] = pawn.mvStartTime[z] + MvDuration( z );

    if ( SvShowCharge_kvar != 0 ) {
        if ( SvShowCharge_kvar == 1 ) {
            DebugSeg( pawn.mvStart[z], pawn.mvEnd[z] );
        } else if ( SvShowCharge_kvar == 2 ) {
            if ( pawn.team[z] == 0 ) {
                DebugSeg( pawn.mvStart[z], pawn.mvEnd[z] );
            }
        } else {
            if ( pawn.team[z] == 1 ) {
                DebugSeg( pawn.mvStart[z], pawn.mvEnd[z] );
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
bool NavUpdate( int z ) {
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

            //SingleShot.Add( dt => {
            //    Hexes.DrawHexWithLines( Draw.GameToScreenPosition( snapA ),
            //                                            Draw.hexPixelSize / 4, Color.white );
            //}, duration: 3 );

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

    pawn.mvStart[z] = pawn.mvEnd[z];
    pawn.mvEnd[z] = AvoidStructure( pawn.team[z], pawn.mvStart[z], HexToV( path[1] ) );

    int leftover = Mathf.Max( 0, ZServer.clock - pawn.mvEndTime[z] );
    pawn.mvStartTime[z] = ZServer.clock;
    pawn.mvEndTime[z] = pawn.mvStartTime[z] + MvDuration( z );

    if ( leftover > 0 ) {
        // advance a bit on the next segment if there is time left from the tick
        if ( ! pawn.MvLerp( z, pawn.mvStartTime[z] + leftover ) ) {
            pawn.mvStart[z] = pawn.mvPos[z];
            pawn.mvEndTime[z] -= leftover;
        }
    }
    return true;
}

// returns true if the segment was corrected (avoidance was done)
bool AvoidStructure( int team, Vector2 v0, Vector2 v1, out Vector2 w ) {
    w = AvoidStructure( team, v0, v1 );
    return ( v1 - w ).sqrMagnitude > 0.0001f;
}

Vector2 AvoidStructure( int team, Vector2 v0, Vector2 v1 ) {
#if true
    return v1;
#else
    Vector2 ab = v1 - v0;
    float sq = ab.sqrMagnitude;
    if ( sq < 0.5f ) {
        return v1;
    }

    float dvLen = Mathf.Sqrt( sq );
    Vector2 abn = ab / dvLen;

    float min = 9999999;

    foreach ( var z in pawn.filter.structures ) {
        // enemy structures are not avoided, get attacked instead
        if ( pawn.team[z] != team ) {
            continue;
        }

        Vector2 c = pawn.mvPos[z];
        Vector2 ac = c - v0;
        Vector2 bc = c - v1;

        float acac = Vector2.Dot( ac, ac );

        // only care about the closest structure intersecting the path
        if ( acac >= min ) {
            continue;
        }

        float sqDist;

        float e = Vector2.Dot( ac, ab );
        if ( e <= 0 ) {
            sqDist = Vector2.Dot( ac, ac );
        } else {
            float f = Vector2.Dot( ab, ab );
            if ( e >= f ) {
                sqDist = Vector2.Dot( bc, bc );
            } else {
                sqDist = acac - e * e / f;
            }
        }

        const float avoidRadius = 1;

        // this structure doesn't intersect the path
        if ( sqDist > avoidRadius * avoidRadius ) {
            continue;
        }

        Vector2 p = Vector2.Perpendicular( abn );

        float sign = Vector2.Dot( ac, p ) >= 0 ? -1 : 1;
        v1 = c + sign * p * avoidRadius * 1.25f;
        min = acac;

#if UNITY_STANDALONE || SDL
        if ( SvShowAvoidance_kvar ) {
            SingleShot.Add( dt => {
                float sz = Draw.hexPixelSize / 4;
                Hexes.DrawHexWithLines( Draw.GTS( c ), sz, Color.green );
                Hexes.DrawHexWithLines( Draw.GTS( v1 ), sz, Color.white );
                QGL.LateDrawLine( Draw.GTS( v0 ), Draw.GTS( v1 ), color: Color.green );
            }, duration: 3 );
        }
#endif
    }
    return v1;
#endif
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
                                                            Draw.hexPixelSize / 2, Color.white );
        }, duration: 1 );
    }
#endif
}


}
