using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

using static Pawn.Def;

partial class Pawn {

public class Filter {
    public List<IList> all;

    public List<byte> garbage = null, no_garbage = null;
    public List<byte> flying = null, no_flying = null;
    public List<byte> structures = null, no_structures = null;

    public Filter() {
        FilterUtil.CreateAll( this, out all );
    }

    public void Assign( int z, bool condition, List<byte> la, List<byte> lb ) {
        var l = condition ? la : lb;
        l.Add( ( byte )z );
    }

    public void Clear() {
        foreach ( var l in all ) {
            l.Clear();
        }
    }
}

public const int MAX_PAWN = 256;

public ushort [] hp = null;
public byte [] team = null;
public byte [] def = null;
public Vector2 [] pos0 = null;
public Vector2 [] pos1 = null;

public int [] pos0_tx = null;
public int [] pos1_tx = null;

public Filter filter = new Filter();

List<Array> _allRows = new List<Array>();

public Pawn() {
    ArrayUtil.CreateAll( this, MAX_PAWN, out _allRows );
    FillDefNames();
}

public void Reset() {
    ArrayUtil.Clear( _allRows );
}

int _lastFree;
public int Create( int pawnDef ) {
    int z;

    if ( ! ArrayUtil.FindFreeColumn( def, out z, _lastFree ) ) {
        return 0;
    }

    ArrayUtil.ClearColumn( _allRows, z );

    def[z] = ( byte )pawnDef;
    hp[z] = ( byte )MaxHP( z );

    return z;
}

public void Destroy( int z ) {
    def[z] = 0;
    _lastFree = z;
}

public int MaxHP( int z ) {
    return defs[def[z]].maxHP;
}

public Def GetDef( int z ) {
    return defs[def[z]];
}

public bool IsFlying( int z ) {
    return ( GetDef( z ).flags & Flags.Flying ) != 0;
}

public bool IsStructure( int z ) {
    return ( GetDef( z ).flags & Flags.Structure ) != 0;
}

public bool IsMoving( int z ) {
    return ( pos0[z] - pos1[z] ).sqrMagnitude > 0.00001f;
}

public bool IsIdling( int z ) {
    return pos0[z] == pos1[z];
}

public bool IsGarbage( int z ) {
    return def[z] == 0;
}

public void UpdateFilters() {
    filter.Clear();

    for ( int z = 0; z < MAX_PAWN; z++ ) {
        filter.Assign( z, IsGarbage( z ), filter.garbage, filter.no_garbage );
    }

    foreach ( int z in filter.no_garbage ) {
        filter.Assign( z, IsFlying( z ), filter.flying, filter.no_flying );
    }

    foreach ( int z in filter.no_flying ) {
        filter.Assign( z, IsStructure( z ), filter.structures, filter.no_structures );
    }
}


}
