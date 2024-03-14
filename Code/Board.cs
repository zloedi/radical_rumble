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

    public int width => size[0];
    public int height => size[1];
    public int numItems => width * height;

    public byte [] size = new byte[2];

    // actual grids
    public byte [] terrain = null;
    public byte [] pawnDef = null;
    public byte [] pawnTeam = null;
    public byte [] zone = null;
    // FIXME: not sure it's place is here
    public byte [] navMap = null;

    public HexPather.Context patherCTX = HexPather.CreateContext( MAX_GRID );
    public List<int> path = new List<int>();
    public List<int> strippedPath = new List<int>();

    List<Array> _allRows = new List<Array>();

    public Board() {
        ArrayUtil.CreateNulls( this, MAX_GRID, out _allRows );
    }

    public void Reset() {
        ArrayUtil.Clear( _allRows );
    }

    public void Clear( int hx ) {
        ArrayUtil.ClearColumn( _allRows, hx );
    }

    //public bool HasFlags( int hx, Flags f ) {
    //    return ( ( Flags )flags[hx] & f ) != 0;
    //}

    //public void RaiseFlags( int hx, Flags f ) {
    //    flags[hx] |= ( byte )f;
    //}

    //public void LowerFlags( int hx, Flags f ) {
    //    flags[hx] &= ( byte )( ~f );
    //}

    public bool IsSolid( int hx ) {
        return terrain[hx] != 0;
    }

    public bool IsSolid( Vector2Int axial ) {
        return IsSolid( Hex( axial ) );
    }

    public bool IsSolid( int x, int y ) {
        return IsSolid( Hex( x, y ) );
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

    public int Distance( int hxA, int hxB ) {
        return Hexes.AxialDistance( Axial( hxA ), Axial( hxB ) );
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
    public bool GetPath( int hxSrc, int hxTarget, int maxPath = 256 ) {
        // fill the obstacle map
        foreach ( var hx in filter.solid ) {
            navMap[hx] = 0;
        }
        foreach ( var hx in filter.no_solid ) {
            navMap[hx] = 1;
        }
        navMap[hxSrc] = 0;

        // flood the map with score
        HexPather.FloodMap( hxSrc, maxPath, width, navMap, numItems, patherCTX );

        // put the geometrical distance in the score
        Vector3Int a = Hexes.AxialToCubeInt( Axial( hxSrc ) );
        foreach ( var hx in filter.solid ) {
            Vector3Int b = Hexes.AxialToCubeInt( Axial( hx ) );
            int dist = ( b - a ).sqrMagnitude;
            patherCTX.floodMap[hx] = ( patherCTX.floodMap[hx] << 16 ) | dist;
        }

        // trace and strip the path
        bool result = HexPather.TracePath( hxTarget, width, patherCTX, path );
        StripPath();

        // the flood is inversed because couldn't make 'simmetrical' paths otherwise
        path.Reverse();
        strippedPath.Reverse();

        return result;
    }

    public void StripPath() {
        strippedPath.Clear();

        if ( path.Count <= 2 ) {
            strippedPath.AddRange( path );
            return;
        }

        strippedPath.Add( path[0] );

        if ( CanReach( path[0], path[path.Count - 1] ) ) {
            strippedPath.Add( path[path.Count - 1] );
            return;
        }

        int start = 0;

        while ( true ) {
            int reach = 0;
            for ( int i = start + 1; i < path.Count; i++ ) {
                if ( CanReach( path[start], path[i] ) ) {
                    if ( CanReach( path[i], path[path.Count - 1] ) ) {
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
        public List<ushort> spawners, no_spawners;

        public Zone [] zones = new Zone[8];

        public Filter() {
            FilterUtil.CreateAll( this, out all );
            for ( int i = 0; i < zones.Length; i++ ) {
                zones[i] = new Zone();
            }
        }

        public void Assign( int hx, bool condition, List<ushort> la, List<ushort> lb ) {
            var l = condition ? la : lb;
            l.Add( ( ushort )hx );
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
            filter.Assign( hx, IsSolid( hx ), filter.solid, filter.no_solid );
        }

        for ( int hx = 0; hx < numItems; hx++ ) {
            filter.Assign( hx, pawnDef[hx] != 0, filter.spawners, filter.no_spawners );
        }

        foreach ( var zn in filter.zones ) {
            zn.team = 0;
            zn.polygon.Clear();
        }

        foreach ( var hx in filter.solid ) {
            ZoneData zd = UnpackZoneData( zone[hx] );
            if ( zd.id == 0 ) {
                continue;
            }
            Zone fz = filter.zones[zd.id];
            int n = zd.polyIdx - ( fz.polygon.Count - 1 );
            for ( int i = 0; i < n; i++ ) {
                fz.polygon.Add( 0 );
            }
            fz.team = zd.team;
            fz.polygon[zd.polyIdx] = hx;
        }

#if false
        foreach ( var zn in filter.zones ) {
            if ( zn.polygon.Count == 0 ) {
                continue;
            }
            string s = "";
            foreach ( var p in zn.polygon ) {
                s += p + ",";
            }
            Qonsole.Log( "list: " + s );
        }
#endif
    }

    public int PathSqDist( List<int> path ) {
        int sqDist = 0;
        for ( int i = 0; i < path.Count - 1; i += 2 ) {
            Vector3Int a = Hexes.AxialToCubeInt( Axial( path[i + 0] ) );
            Vector3Int b = Hexes.AxialToCubeInt( Axial( path[i + 1] ) );
            sqDist += ( b - a ).sqrMagnitude;
        }
        return sqDist;
    }

    public bool CanReach( int hxA, int hxB ) {
        if ( hxA == hxB ) {
            return true;
        }
        return CanReach( Axial( hxA ), Axial( hxB ) );
    }

    public bool CanReach( Vector2Int axialA, Vector2Int axialB ) {
        if ( axialA == axialB ) {
            return true;
        }

        bool isBlocking( Vector2Int axial ) {
            return navMap[Hex( axial )] != 0;
        }

        Vector3 cubeA = Hexes.AxialToCube( axialA );
        Vector3 cubeB = Hexes.AxialToCube( axialB );

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

    // filtered out from hexes
    public class Zone {
        public int team;
        public List<ushort> polygon = new List<ushort>();
    }

    // stored in a hex
    public struct ZoneData {
        public int team;
        public int id;
        public int polyIdx;
    }

    public byte PackZoneData( ZoneData zd ) {
        return ( byte )( 0
            | ( ( zd.team    & 1  ) << 7 )
            | ( ( zd.id      & 7  ) << 4 )
            | ( ( zd.polyIdx & 15 ) << 0 )
        );
    }

    public ZoneData UnpackZoneData( byte b ) {
        return new ZoneData {
            team =    ( b >> 7  ) & 1,
            id =      ( b >> 4  ) & 7,
            polyIdx = ( b >> 0  ) & 15,
        };
    }
}
