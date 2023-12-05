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


public void TickServer( int dt ) {
    pawn.UpdateFilters();

    float dtSecs = dt / 1000f;

    foreach ( var z in pawn.filter.no_moving ) {
        int dest = 176;

        Vector2Int axial = Hexes.ScreenToHex( pawn.pos0[z] );
        GetCachedPath( board.Hex( axial ), dest, out List<int> path );
        if ( path.Count > 1 ) {
            pawn.pos1[z] = Hexes.HexToScreen( board.Axial( path[1] ) );
        }

        List<Vector2> pathLine = new List<Vector2>();
        SingleShot.Add( deltat => {
            pathLine.Clear();
            foreach ( var hx in path ) {
                pathLine.Add( Draw.HexToScreen( hx ) );
            }
            QGL.LateDrawLine( pathLine );
        } );
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

public void Spawn( int def, float x, float y ) {
    int z = pawn.Create( def );
    if ( z == 0 ) {
        Error( "Out of pawns, can't create." );
        return;
    }
    pawn.pos1[z] = pawn.pos0[z] = new Vector2( x, y );
    Log( $"Spawned {Pawn.defs[def].name} at idx: {z} pos: {pawn.pos0[z]}" );
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

Dictionary<int,List<int>> _pathCache = new Dictionary<int,List<int>>();
void GetCachedPath( int hxSrc, int hxTarget, out List<int> path ) {
    int key = ( hxSrc << 16 ) | hxTarget;
    if ( _pathCache.TryGetValue( key, out path ) ) {
        return;
    }
    Qonsole.Log( "[ffc000]Casting the real pather...[-]" );
    board.GetPath( hxSrc, hxTarget );
    path = new List<int>( board.strippedPath );
    _pathCache[key] = path;
}


}
