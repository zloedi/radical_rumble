using System;
using System.Collections;
using System.Collections.Generic;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

public class Board {

    public const int MAX_GRID = 4 * 1024;

    [Flags]
    public enum Flags {
        Tower = 1 << 0,
    }
    
    public int width => size[0];
    public int height => size[1];
    public int numItems => width * height;

    public byte [] size = new byte[2];

    public byte [] terrain = new byte[MAX_GRID];
    public byte [] flags = new byte[MAX_GRID];

    public byte [] navMap = new byte[MAX_GRID];
    public HexPather.Context patherCTX = HexPather.CreateContext( MAX_GRID );
    public List<int> path = new List<int>();
    public List<int> strippedPath = new List<int>();

    public bool HasFlags( int hx, Flags f ) {
        return ( ( Flags )flags[hx] & f ) != 0;
    }

    public void RaiseFlags( int hx, Flags f ) {
        flags[hx] |= ( byte )f;
    }

    public void LowerFlags( int hx, Flags f ) {
        flags[hx] &= ( byte )( ~f );
    }

    public bool IsSolid( int hx ) {
        return terrain[hx] != 0;
    }

    public bool IsSolid( Vector2Int axial ) {
        return IsSolid( Hex( axial ) );
    }

    public int Clamp( int hx ) {
        return Mathf.Clamp( hx, 0, numItems - 1 );
    }

    public bool IsInBounds( int hx ) {
        return hx >= 0 && hx < numItems;
    }

    public bool IsInBounds( Vector2Int hxc ) {
        return hxc.x >= 0 && hxc.x < width && hxc.y >= 0 && hxc.y < height;
    }

    public Vector2Int Axial( int hx ) {
        int w = width == 0 ? 256 : width;
        return new Vector2Int( hx % w, hx / w );
    }

    public int Hex( Vector2Int axial ) {
        return Hex( axial.x, axial.y );
    }

    public int Hex( int x, int y ) {
        return x + y * width;
    }

    // hxTarget may be a void hex
    public void GetPath( int hxSrc, int hxTarget ) {
        // fill the obstacle map
        Array.Clear( navMap, 0, numItems );
        foreach ( var hx in filter.no_solid ) {
            navMap[hx] = 1;
        }
        navMap[hxTarget] = 0;

        // flood the map with score
        HexPather.FloodMap( hxTarget, 256, width, navMap, numItems, patherCTX );

        // put the geometrical distance in the score
        foreach ( var hx in filter.solid ) {
            Vector3Int a = Hexes.AxialToCubeInt( Axial( hxTarget ) );
            Vector3Int b = Hexes.AxialToCubeInt( Axial( hx ) );
            int dist = ( b - a ).sqrMagnitude;
            patherCTX.floodMap[hx] = ( patherCTX.floodMap[hx] << 16 ) | dist;
        }

        // trace and strip the path
        HexPather.TracePath( hxSrc, width, patherCTX, path );
        StripPath();
    }

    public void StripPath() {
        strippedPath.Clear();

        if ( path.Count <= 2 ) {
            strippedPath.AddRange( path );
            return;
        }

        strippedPath.Add( path[0] );

        if ( CanReach( path[0], path[path.Count - 1], navMap ) ) {
            strippedPath.Add( path[path.Count - 1] );
            return;
        }

        int start = 0;

        while ( true ) {
            int reach = 0;
            for ( int i = start + 1; i < path.Count; i++ ) {
                if ( CanReach( path[start], path[i], navMap ) ) {
                    if ( CanReach( path[i], path[path.Count - 1], navMap ) ) {
                        strippedPath.Add( path[i] );
                        reach = path.Count - 1;
                        break;
                    }
                    reach = i;
                }
            }
            if ( reach > 0 ) {
                strippedPath.Add( path[reach] );
                start = reach;
                if ( start == path.Count - 1 ) {
                    break;
                }
            }
        }
    }

    public class Filter {
        public List<IList> all;
        public List<ushort> solid, no_solid;

        public Filter() {
            FilterUtil.CreateAll( this, out all );
        }

        public void Clear() {
            foreach ( var l in all ) {
                l.Clear();
            }
        }
    }

    public Filter filter = new Filter();

    public void UpdateFilters() {
        filter.Clear();

        for ( int hx = 0; hx < numItems; hx++ ) {
            var l = IsSolid( hx ) ? filter.solid : filter.no_solid;
            l.Add( ( ushort )hx );
        }
    }

    bool CanReach( int hxA, int hxB, byte [] navMap ) {
        if ( hxA == hxB ) {
            return true;
        }

        bool isBlocking( Vector2Int axial ) {
            return navMap[Hex( axial )] != 0;
        }

        Vector3 cubeA = Hexes.AxialToCube( Axial( hxA ) );
        Vector3 cubeB = Hexes.AxialToCube( Axial( hxB ) );
        float n = Hexes.CubeDistance( cubeA, cubeB );
        float step = 1f / n;
        for ( float i = 0; i <= n; i++ ) {
            Vector3 c = Vector3.Lerp( cubeA, cubeB, i * step );
            Vector3 cr = Hexes.CubeRound( c );
            Vector3 d = c - cr;

            {
                Vector2Int ax = Hexes.CubeToAxial( cr );
                if ( isBlocking( ax ) ) {
                    return false;
                }
            }

            const float eps = 0.49f;
            if ( d.x > eps * eps ) {
                c.x += 0.5f;
                Vector2Int ax = Hexes.CubeToAxial( Hexes.CubeRound( c ) );
                if ( isBlocking( ax ) ) {
                    return false;
                }
            } else if ( d.y > eps * eps ) {
                c.y += 0.5f;
                Vector2Int ax = Hexes.CubeToAxial( Hexes.CubeRound( c ) );
                if ( isBlocking( ax ) ) {
                    return false;
                }
            } else if ( d.z > eps * eps ) {
                c.z += 0.5f;
                Vector2Int ax = Hexes.CubeToAxial( Hexes.CubeRound( c ) );
                if ( isBlocking( ax ) ) {
                    return false;
                }
            }
        }
        return true;
    }
}
