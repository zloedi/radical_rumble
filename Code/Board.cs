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

    public int Clamp( int hx ) {
        return Mathf.Clamp( hx, 0, numItems - 1 );
    }

    public bool IsInBounds( int hx ) {
        return hx >= 0 && hx < numItems;
    }

    public bool IsInBounds( Vector2Int hxc ) {
        return hxc.x >= 0 && hxc.x < width && hxc.y >= 0 && hxc.y < height;
    }

    public Vector2Int HexToCoord( int hx ) {
        int w = width == 0 ? 256 : width;
        return new Vector2Int( hx % w, hx / w );
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
}
