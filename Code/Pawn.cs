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

    public List<byte> idling = null, no_idling = null;
    public List<byte> [] enemies = new List<byte>[2];
    public List<byte> [] team = new List<byte>[2];

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

// lerped movement position
public Vector2 [] mvPos = null;
// target movement position
public Vector2 [] mvEnd = null;
public byte [] mvPawn = null;
public int [] mvStartTime = null;

// these are synced
// transmitted fixed point version of end position
public int [] mvEnd_tx = null;
public int [] mvEndTime = null;

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

public int Speed( int z ) {
    return GetDef( z ).speed;
}

public int MaxHP( int z ) {
    return defs[def[z]].maxHP;
}

public Def GetDef( int z ) {
    return defs[def[z]];
}

public bool IsFlying( int z ) {
    return ( GetDef( z ).flags & Pawn.Def.Flags.Flying ) != 0;
}

public bool IsStructure( int z ) {
    return ( GetDef( z ).flags & Pawn.Def.Flags.Structure ) != 0;
}

public bool IsIdling( int z ) {
    // if we haven't transmitted any positions yet, don't try to interpolate paths
    if ( mvEnd_tx[z] == 0 ) {
        return false;
    }

    return mvPawn[z] == 0;
}

public bool IsGarbage( int z ) {
    return def[z] == 0;
}

public bool UpdateMovementPosition( int z, int clock ) {
    int duration = mvEndTime[z] - mvStartTime[z];
    if ( duration <= 0 ) {
        return true;
    }

    int ti = mvEndTime[z] - clock;
    if ( ti <= 0 ) {
        return true;
    }

    if ( mvPos[z] == Vector2.zero ) {
        mvPos[z] = mvEnd[z];
        return true;
    }

    Vector2 d = mvEnd[z] - mvPos[z];
    float sq = d.sqrMagnitude;
    if ( sq < 0.00001f ) {
        return true;
    }

    float speed = Speed( z ) / 60f;

    d /= Mathf.Sqrt( sq );
    d *= speed * ti / 1000f;

    mvPos[z] = mvEnd[z] - d;
    return false;
}

public void UpdateFilters() {
    filter.Clear();

    for ( int z = 0; z < MAX_PAWN; z++ ) {
        filter.Assign( z, IsGarbage( z ), filter.garbage, filter.no_garbage );
    }

    foreach ( int z in filter.no_garbage ) {
        filter.Assign( z, team[z] == 0, filter.team[0], filter.team[1] );
    }

    foreach ( int z in filter.no_garbage ) {
        filter.Assign( z, team[z] == 0, filter.enemies[1], filter.enemies[0] );
    }

    foreach ( int z in filter.no_garbage ) {
        filter.Assign( z, IsFlying( z ), filter.flying, filter.no_flying );
    }

    foreach ( int z in filter.no_flying ) {
        filter.Assign( z, IsStructure( z ), filter.structures, filter.no_structures );
    }
    
    foreach ( int z in filter.flying ) {
        filter.Assign( z, IsIdling( z ), filter.idling, filter.no_idling );
    }

    foreach ( int z in filter.no_structures ) {
        filter.Assign( z, IsIdling( z ), filter.idling, filter.no_idling );
    }
}


}
