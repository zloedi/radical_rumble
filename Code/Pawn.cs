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
public Vector2 [] mvEnd = null;
public Vector2 [] mvStart = null;
public byte [] mvPawn = null;
public int [] mvStartTime = null;

// these should be synced

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

public void Clear( int z ) {
    ArrayUtil.ClearColumn( _allRows, z );
}

int _lastFree;
public int Create( int pawnDef ) {
    int z;

    if ( ! ArrayUtil.FindFreeColumn( def, out z, _lastFree ) ) {
        return 0;
    }

    Clear( z );

    def[z] = ( byte )pawnDef;
    hp[z] = ( byte )MaxHP( z );

    return z;
}

public void Destroy( int z ) {
    Clear( z );
    _lastFree = z;
}

public float SpeedSec( int z ) {
    return Speed( z ) / 60f;
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
    // FIXME: this is a temp solution
    if ( mvEnd_tx[z] == 0 ) {
        return false;
    }

    return mvPawn[z] == 0;
}

public bool IsGarbage( int z ) {
    return def[z] == 0;
}

public bool LerpMovePosition( int z, int clock ) {
    int duration = mvEndTime[z] - mvStartTime[z];
    if ( duration <= 0 ) {
        return true;
    }

    if ( clock >= mvEndTime[z] ) {
        return true;
    }

    int ti = clock - mvStartTime[z];
    float t = ( float )ti / duration;
    mvPos[z] = Vector2.Lerp( mvStart[z], mvEnd[z], t );
    return false;
}

public bool SpeculateMovementPosition( int z, int clock, int deltaTime ) {
    if ( mvPos[z] == Vector2.zero ) {
        mvPos[z] = mvStart[z] = mvEnd[z];
        mvStartTime[z] = mvEndTime[z] = clock;
        return true;
    }

    int duration = mvEndTime[z] - mvStartTime[z];
    if ( duration <= 0 ) {
        return true;
    }

    duration = mvEndTime[z] - mvStartTime[z];

    if ( clock >= mvEndTime[z] ) {
        Vector2 v = mvEnd[z] - mvStart[z];
        float sq = v.sqrMagnitude;
        if ( sq < 0.0001f ) {
            return true;
        }

        // keep moving in the same general direction until the server correction arrives
        // this really craps-up for faster pawns, but it is ok for almost everything
        v /= Mathf.Sqrt( sq );
        Vector2 newPos = mvPos[z] + v * SpeedSec( z ) * deltaTime / 1000f;
        if ( ( newPos - mvEnd[z] ).sqrMagnitude > SpeedSec( z ) ) {
            // stop if too far from the destination
            mvPos[z] = mvEnd[z];
            return true;
        }

        mvPos[z] = newPos;
        return false;
    }

    int ti = clock - mvStartTime[z];
    float t = ( float )ti / duration;

    mvPos[z] = Vector2.Lerp( mvStart[z], mvEnd[z], t );

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
