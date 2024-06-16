using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

namespace RR {


using static Pawn.Def;
using PDF = RR.Pawn.Def.Flags;


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
        Spawn       = 1 << 0,
        Move        = 1 << 1,
        Attack      = 1 << 2,
        HurtVisuals = 1 << 3,
    }

    public static readonly State [] AllStates = ( State[] )Enum.GetValues( typeof( State ) );

    public const int MAX_PAWN = 256;
    public const float ATK_MIN_DIST = 0.45f;

    public ushort [] hp = null;
    public byte [] team = null;
    public byte [] def = null;

    // movement position (current)
    public Vector2 [] mvPos = null;
    // movement position (target)
    public Vector2 [] mvEnd = null;

    // used on the client to lerp
    public Vector2 [] mvStart = null;
    public int [] mvStart_ms = null;

    public byte [] state = null;

    // == these should be synced ==

    // the pawn of interest for navigation and attack
    public byte [] focus = null;

    // transmitted fixed point version of end position
    public int [] mvEnd_tx = null;

    // transmitted arrival timestamp
    public int [] mvEnd_ms = null;

    // impact/take damage timestamp
    public int [] atkEnd_ms = null;

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

    public int Cost( int z ) {
        return defs[def[z]].cost;
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
        return ( GetDef( z ).flags & PDF.Flying ) != 0;
    }

    public bool IsStructure( int z ) {
        return ( GetDef( z ).flags & PDF.Structure ) != 0;
    }

    public bool IsWinObjective( int z ) {
        return ( GetDef( z ).flags & PDF.WinObjective ) != 0;
    }

    public bool IsPatrolWaypoint( int z ) {
        return ( GetDef( z ).flags & PDF.PatrolWaypoint ) != 0;
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

    // instantly teleport to end position
    // this will potentially generate a delta (clock change)
    public void MvSnapToEnd( int z ) {
        MvSnapToEnd( z, 0 );
    }

    // this will potentially generate a delta (clock change)
    public void MvSnapToEnd( int z, int clock ) {
        mvPos[z] = mvEnd[z];
        mvEnd_ms[z] = clock;
    }

    // snap endpos to current pos and reset the clock
    // this will generate a delta (clock change)
    // on the client, this is a nice stop signal
    public void MvInterrupt( int z, int clock ) {
        mvEnd[z] = mvPos[z];
        mvEnd_ms[z] = clock;
    }

    // don't generate delta if already on the spot
    public void MvInterruptSoft( int z, int clock ) {
        if ( mvEnd[z] != mvPos[z] ) {
            mvEnd[z] = mvPos[z];
            mvEnd_ms[z] = clock;
        }
    }

    public void MvLerpClient( int z, int clock, float deltaTime ) {
        if ( mvPos[z] == Vector2.zero ) {
            mvPos[z] = mvStart[z] = mvEnd[z];
            mvStart_ms[z] = mvEnd_ms[z] = clock;
            return;
        }

        int duration = mvEnd_ms[z] - mvStart_ms[z];
        if ( duration <= 0 ) {
            // FIXME: lerp mvpos to end if they differ
            return;
        }

        if ( clock >= mvEnd_ms[z] ) {
            Vector2 v = mvEnd[z] - mvStart[z];
            float sq = v.sqrMagnitude;
            if ( sq < 0.0001f ) {
                return;
            }

            // keep moving in the same general direction until the server correction arrives
            // this really craps-up for faster pawns, but it is ok for almost everything
            v /= Mathf.Sqrt( sq );
            Vector2 newPos = mvPos[z] + v * SpeedSec( z ) * deltaTime;
            if ( ( newPos - mvEnd[z] ).sqrMagnitude > SpeedSec( z ) ) {
                // stop if too far from the destination
                mvPos[z] = mvEnd[z];
                return;
            }

            mvPos[z] = newPos;
            return;
        }

        int ti = clock - mvStart_ms[z];
        float t = ( float )ti / duration;

        mvPos[z] = Vector2.Lerp( mvStart[z], mvEnd[z], t );
    }

    public bool MvChaseEndPoint( int z, int clock ) {
        if ( clock >= mvEnd_ms[z] ) {
            return false;
        }

        Vector2 v = mvPos[z] - mvEnd[z];
        float sq = v.sqrMagnitude;
        if ( sq < 0.000001f ) {
            MvSnapToEnd( z, clock );
            return false;
        }

        v /= Mathf.Sqrt( sq );
        float timeLeft = ( mvEnd_ms[z] - clock ) / 1000f;
        mvPos[z] = mvEnd[z] + v * SpeedSec( z ) * timeLeft;

        return true;
    }

    public bool MvLerp( int z, int clock ) {
        return ! MvChaseEndPoint( z, clock );
    }

    public float DistanceForAttack( int zAttacker, int zDefender ) {
        return Radius( zAttacker ) + Radius( zDefender )
                + Mathf.Max( ATK_MIN_DIST, Range( zAttacker ) );
    }

    public class Filter {
        public List<IList> all;

        public List<byte> garbage = null, no_garbage = null;
        public List<byte> flying = null, no_flying = null;
        public List<byte> structures = null, no_structures = null;

        public List<byte> [] enemies = new List<byte>[2];
        public List<byte> [] team = new List<byte>[2];
        public List<byte> [] objectives = new List<byte>[2];
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

        foreach ( int z in filter.no_garbage ) {
            if ( IsWinObjective( z ) ) {
                filter.Assign( z, team[z] == 0, filter.objectives[0], filter.objectives[1] );
            }
        }
    }
}


} // namespace
