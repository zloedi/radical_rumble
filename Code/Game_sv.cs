using System;
using System.Collections.Generic;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

using static Pawn.Def;

using Sv = RRServer;

partial class Game {

//static bool SvShowPaths_kvar = false;

public void TickServer() {
    pawn.UpdateFilters();
    RegisterIntoGrids();
    foreach ( var z in pawn.filter.no_garbage ) {
        pawn.pos0_tx[z] = ToTx( pawn.pos0[z] );
        pawn.pos1_tx[z] = ToTx( pawn.pos1[z] );
    }
}

#if false
public void TickServer_( int deltaTimeMS ) {
    float dtSecs = deltaTimeMS / 1000f;

    pawn.UpdateFilters();
    RegisterIntoGrids();

    int newMovers = 0;
    foreach ( var z in pawn.filter.no_moving ) {
        int dest = 176;

        GetCachedPath( VToHex( pawn.pos0[z] ), dest, out List<int> path );
        if ( path.Count > 1 ) {
            pawn.pos1[z] = HexToV( path[1] );
            int segmentDist = ToTx( ( pawn.pos1[z] - pawn.pos0[z] ).magnitude );
            int duration = ( 60 * segmentDist / pawn.GetDef( z ).speed * 1000 ) >> FRAC_BITS;
            newMovers++;
        }

        if ( SvShowPaths_kvar ) {
#if UNITY_STANDALONE
            List<Vector2> pathLine = new List<Vector2>();
            pathLine.Clear();
            foreach ( var hx in path ) {
                pathLine.Add( Draw.HexToScreen( hx ) );
            }
            SingleShot.Add( dt => {
                QGL.LateDrawLine( pathLine );
            } );
#endif
        }
    }

    if ( newMovers > 0 ) {
        pawn.UpdateFilters_moving();
    }

    foreach ( var z in pawn.filter.moving ) {
        //pawn.posT[z] += dtSecs * pawn.GetDef( z ).speed;
        //pawn.posT[z] = Mathf.Min( pawn.posT[z], 1 );
        //pawn.pos[z] = Vector2.Lerp( pawn.pos0[z], pawn.pos1[z], pawn.posT[z] );
    }

    foreach ( var z in pawn.filter.no_garbage ) {
        pawn.pos0_tx[z] = ToTx( pawn.pos0[z] );
        pawn.pos1_tx[z] = ToTx( pawn.pos1[z] );
    }
}
#endif

public void Spawn( int def, float x, float y ) {
    int z = pawn.Create( def );
    if ( z == 0 ) {
        Error( "Out of pawns, can't create." );
        return;
    }
    Log( $"Spawned {Pawn.defs[def].name} at idx: {z} pos: {pawn.pos0[z]}" );
    pawn.pos1[z] = pawn.pos0[z] = new Vector2( x, y );
    if ( pawn.IsStructure( z ) ) {
        int hx = VToHex( pawn.pos0[z] );
        board.pawnDef[hx] = pawn.def[z];
        Log( $"Placing a structure on the grid." );
    }
}

public void Kill( int z ) {
    if ( ( z < 1 && z >= Pawn.MAX_PAWN ) || pawn.IsGarbage( z ) ) {
        Error( $"Invalid pawn {z}" );
        return;
    }
    if ( pawn.IsStructure( z ) ) {
        int hx = VToHex( pawn.pos0[z] );
        board.pawnDef[hx] = 0;
        Log( $"Removing a structure from the grid." );
    }
    pawn.Destroy( z );
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

// target can be a void hex bordering the solids
Dictionary<int,List<int>> _pathCache = new Dictionary<int,List<int>>();
void GetCachedPath( int hxSrc, int hxTarget, out List<int> path ) {
    int key = ( hxSrc << 16 ) | hxTarget;
    if ( _pathCache.TryGetValue( key, out path ) ) {
        return;
    }

    Qonsole.Log( $"[ffc000]Casting the real pather. Num paths in cache: {_pathCache.Count}[-]" );
    board.GetPath( hxSrc, hxTarget );

    // each segment is a valid path in both ways
    CachePathSubpaths( hxSrc, hxTarget, board.strippedPath );

    path = _pathCache[key];
}

void CachePathSubpaths( int hxA, int hxB, List<int> path ) {
    CachePathBothWays( hxA, hxB, path );
    
    for ( int i = 0; i < path.Count - 1; i++ ) {
        int i0 = i + 0;
        int i1 = i + 1;
        var seg = new List<int>() { path[i0], path[i1] };
        CachePathBothWays( seg[0], seg[1], seg );
    }

    var p = new List<int>( path );
    for ( int i = p.Count - 1; i >= 3; i-- ) {
        p.RemoveAt( i );
        CachePathBothWays( p[0], p[p.Count-1], p );
    }

    p = new List<int>( path );
    p.Reverse();
    for ( int i = p.Count - 1; i >= 3; i-- ) {
        p.RemoveAt( i );
        CachePathBothWays( p[0], p[p.Count-1], p );
    }
}

void CachePathBothWays( int hxA, int hxB, List<int> path ) {
    int key0 = ( hxA << 16 ) | hxB;
    _pathCache[key0] = new List<int>( path );

    int key1 = ( hxB << 16 ) | hxA;
    _pathCache[key1] = new List<int>( path );
    _pathCache[key1].Reverse();
}


}
