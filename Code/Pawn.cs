using System;
using System.Collections;
using System.Collections.Generic;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

using static PawnDef;

class Pawn {

public class Filter {
    public List<IList> all;

    public List<ushort> garbage = null, no_garbage = null;

    public Filter() {
        FilterUtil.CreateAll( this, out all );
    }

    public void Clear() {
        foreach ( var l in all ) {
            l.Clear();
        }
    }
}

public const int MAX_PAWN = 256;

public byte [] hp = RegisterRow<byte>();
public byte [] def = RegisterRow<byte>();
public Vector2 [] pos0 = RegisterRow<Vector2>();
public Vector2 [] pos1 = RegisterRow<Vector2>();
public int [] pos0_tx = RegisterRow<int>();
public int [] pos1_tx = RegisterRow<int>();

public Filter filter = new Filter();

static List<Array> _allRows = new List<Array>();

public void Reset() {
    foreach ( var r in _allRows ) {
        Array.Clear( r, 0, r.Length );
    }
}

int _createSeq;
public int Create( int pawnDef ) {
    int z;

    if ( _createSeq != 0 && IsGarbage( _createSeq ) ) {
        z = _createSeq;
    } else {
        for ( z = 1; z < MAX_PAWN; z++ ) {
            if ( IsGarbage( z ) ) {
                break;
            }
        }
        if ( z == MAX_PAWN ) {
            return 0;
        }
    }

    _createSeq = ( z + 1 ) & MAX_PAWN - 1;

    foreach ( var r in _allRows ) {
        Array.Clear( r, z, 1 );
    }

    def[z] = ( byte )pawnDef;
    hp[z] = ( byte )MaxHP( z );

    return z;
}

public int MaxHP( int z ) {
    return defs[def[z]].maxHP;
}

public void Destroy( int z ) {
    _createSeq = z;
    def[z] = 0;
}

public bool IsGarbage( int z ) {
    return def[z] == 0;
}

public void UpdateFilters() {
    filter.Clear();
    for ( int z = 0; z < MAX_PAWN; z++ ) {
        var l = IsGarbage( z ) ? filter.garbage : filter.no_garbage;
        l.Add( ( ushort )z );
    }
}

static T [] RegisterRow<T>() {
    var r = new T[MAX_PAWN];
    _allRows.Add( r );
    return r; 
}


}
