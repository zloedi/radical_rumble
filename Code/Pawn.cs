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
    public enum State {
        None,
        Spawning,
        Idle,

        // usually move to enemy tower
        Patrol,

        ChargeEnemy,
        Attack,

        Dead,
    }

    // flags that live for a single tick on the client
    public enum ClientTrigger {
        None,
        Move        = 1 << 0,
        Attack      = 1 << 1,
        HurtVisuals = 1 << 2,
    }

    public static readonly State [] AllStates = ( State[] )Enum.GetValues( typeof( State ) );

    public const int MAX_PAWN = 256;

    public ushort [] hp = null;
    public byte [] team = null;
    public byte [] def = null;

    // lerped movement position
    public Vector2 [] mvPos = null;
    public Vector2 [] mvEnd = null;
    public Vector2 [] mvStart = null;
    public int [] mvStartTime = null;

    //public float [] atkPos = null;
    //public int [] atkStartTime = null;

    public byte [] state = null;

    // == these should be synced ==

    // the pawn of interest for navigation and attack
    public byte [] focus = null;

    // transmitted fixed point version of end position
    public int [] mvEnd_tx = null;
    public int [] mvEndTime = null;

    // time of impact/take damage
    public int [] atkEndTime = null;

    public Filter filter = new Filter();

    List<Array> _allRows;

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

    public static byte SB( State s ) {
        return ( byte )s;
    }

    public State GetState( int z ) {
        return ( State )state[z];
    }

    public void SetState( int z, State s ) {
        state[z] = SB( s );
    }

    int _lastFree;
    public int Create( int pawnDef ) {
        int z;

        if ( ! ArrayUtil.FindFreeColumn( def, out z, _lastFree ) ) {
            return 0;
        }

        Clear( z );

        def[z] = ( byte )pawnDef;
        hp[z] = ( ushort )MaxHP( z );

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

    public float Radius( int z ) {
        return defs[def[z]].radius;
    }

    public float Range( int z ) {
        return defs[def[z]].range;
    }

    public int AttackTime( int z ) {
        return defs[def[z]].attackTime;
    }

    public int LoadTime( int z ) {
        return defs[def[z]].loadTime;
    }

    public int Damage( int z ) {
        return defs[def[z]].damage;
    }

    public int MaxHP( int z ) {
        return defs[def[z]].maxHP;
    }

    public Def GetDef( int z ) {
        return defs[def[z]];
    }

    public string DN( int z ) {
        return DebugName( z );
    }

    public string DebugName( int z ) {
        return $"{GetDef( z ).name} {z}";
    }

    public bool IsFlying( int z ) {
        return ( GetDef( z ).flags & Pawn.Def.Flags.Flying ) != 0;
    }

    public bool IsStructure( int z ) {
        return ( GetDef( z ).flags & Pawn.Def.Flags.Structure ) != 0;
    }

    public bool IsWinObjective( int z ) {
        return ( GetDef( z ).flags & Pawn.Def.Flags.WinObjective ) != 0;
    }

    public bool IsPatrolWaypoint( int z ) {
        return ( GetDef( z ).flags & Pawn.Def.Flags.PatrolWaypoint ) != 0;
    }

    // FIXME: no longer valid
    public bool IsIdling( int z ) {
        // if we haven't transmitted any positions yet, don't try to interpolate paths
        // FIXME: this is a temp solution
        if ( mvEnd_tx[z] == 0 ) {
            return false;
        }

        return focus[z] == 0;
    }

    public bool IsEnemy( int z, int zEnemy ) {
        return team[z] != team[zEnemy];
    }

    public bool IsDead( int z ) {
        return state[z] == SB( State.Dead );
    }

    public bool IsGarbage( int z ) {
        return def[z] == 0;
    }

    public float SqDist( int zA, int zB ) {
        return ( mvPos[zB] - mvPos[zA] ).sqrMagnitude;
    }

    public void MvClamp( int z ) {
        mvStart[z] = mvPos[z] = mvEnd[z];
    }

    // this will generate a delta (clock change)
    public void MvSnapToEnd( int z, int clock ) {
        mvStartTime[z] = mvEndTime[z] = clock;
        MvClamp( z );
    }

    // this will generate a delta (clock change)
    public void MvInterrupt( int z, int clock ) {
        mvStartTime[z] = mvEndTime[z] = clock;
        mvStart[z] = mvEnd[z] = mvPos[z];
    }

    public bool MvLerp( int z, int clock ) {
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

    //public bool AtkLerp( int z, int clock ) {
    //    int duration = atkEndTime[z] - atkStartTime[z];
    //    if ( duration <= 0 ) {
    //        return true;
    //    }

    //    if ( clock >= atkEndTime[z] ) {
    //        return true;
    //    }

    //    int ti = clock - atkStartTime[z];
    //    atkPos[z] = ( float )ti / duration;

    //    return false;
    //}

    public bool SpeculateMovementPosition( int z, int clock, int deltaTime ) {
        if ( mvPos[z] == Vector2.zero ) {
            mvPos[z] = mvStart[z] = mvEnd[z];
            mvStartTime[z] = mvEndTime[z] = clock;
            return true;
        }

        int duration = mvEndTime[z] - mvStartTime[z];
        if ( duration <= 0 ) {
            // FIXME: lerp mvpos to end if they differ
            return true;
        }

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

    public class Filter {
        public List<IList> all;

        public List<byte> garbage = null, no_garbage = null;
        public List<byte> flying = null, no_flying = null;
        public List<byte> structures = null, no_structures = null;

        // FIXME: obsolete
        public List<byte> [] enemies = new List<byte>[2];
        public List<byte> [] team = new List<byte>[2];
        public List<byte> [] byState = new List<byte>[AllStates.Length];
        public List<byte> [] no_byState = new List<byte>[AllStates.Length];

        public List<byte> alive => ByStateNot( State.Dead );
        public List<byte> no_alive => ByState( State.Dead );

        public Filter() {
            FilterUtil.CreateAll( this, out all );
        }

        public List<byte> ByState( State state ) {
            return byState[( int )state];
        }

        public List<byte> ByStateNot( State state ) {
            return no_byState[( int )state];
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

    public void UpdateFilters() {
        filter.Clear();

        for ( int z = 0; z < MAX_PAWN; z++ ) {
            filter.Assign( z, IsGarbage( z ), filter.garbage, filter.no_garbage );
        }

        foreach ( int z in filter.no_garbage ) {
            foreach ( var s in Pawn.AllStates ) {
                int sb = SB( s );
                filter.Assign( z, state[z] == sb, filter.byState[sb], filter.no_byState[sb] );
            }
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
    }
}
