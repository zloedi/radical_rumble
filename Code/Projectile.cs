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


class Projectile {
    public const int MAX_PROJECTILE = 256;

    public byte [] zSrc = null;
    public byte [] zDst = null;
    public Vector3 [] posCur = null;
    public Vector3 [] posStart = null;
    public Vector3 [] posEnd = null;
    public Vector3 [] forward = null;

    // timestamps in milliseconds
    public int [] msStart = null;
    public int [] msEnd = null;

    public int [] id = null;
    
    public Filter filter = new Filter();

    List<Array> _allRows;

    int _numSpawned = 0;

    public Projectile() {
        ArrayUtil.CreateAll( this, MAX_PROJECTILE, out _allRows );
    }

    public void Reset() {
        ArrayUtil.Clear( _allRows );
    }

    public void Clear( int pj ) {
        ArrayUtil.ClearColumn( _allRows, pj );
    }

    int _lastFree;
    public int Create() {
        if ( ! ArrayUtil.FindFreeColumn( msStart, out int pj, _lastFree ) ) {
            return 0;
        }
        Clear( pj );
        _numSpawned++;
        id[pj] = _numSpawned;
        forward[pj] = new Vector3( 0, 0, 1 );
        return pj;
    }

    public void Destroy( int pj ) {
        msEnd[pj] = msStart[pj] = 0;
        _lastFree = pj;
    }

    public bool Lerp( int pj, int clock ) {
        if ( clock >= msEnd[pj] ) {
            posStart[pj] = posCur[pj] = posEnd[pj];
            msStart[pj] = msEnd[pj];
            return false;
        }
        float t = ( float )( clock - msStart[pj] ) / ( msEnd[pj] - msStart[pj] );
        posCur[pj] = Vector3.Lerp( posStart[pj], posEnd[pj], t );
        Vector3 fwd = Vector3.Lerp( posStart[pj], posEnd[pj], t + 0.1f ) - posCur[pj];
        forward[pj] = fwd.sqrMagnitude > 0.0001f ? fwd : forward[pj];
        return true;
    }

    public bool IsTravelling( int pj ) {
        return msStart[pj] < msEnd[pj];
    }

    public void UpdateFilters() {
        filter.Clear();

        for ( int pj = 0; pj < MAX_PROJECTILE; pj++ ) {
            filter.Assign( pj, IsGarbage( pj ), filter.garbage, filter.no_garbage );
            filter.Assign( pj, IsTravelling( pj ), filter.travel, filter.no_travel );
        }
    }
    
    public bool IsGarbage( int pj ) {
        return msStart[pj] == 0;
    }

    public void StopTracking( int pj ) {
        zSrc[pj] = zDst[pj] = 0;
    }

    public bool ShouldKeepTracking( int pj ) {
        return zSrc[pj] != 0 && zDst[pj] != 0;
    }

    public class Filter {
        public List<IList> all;

        public List<byte> garbage = null, no_garbage = null;
        public List<byte> travel = null, no_travel = null;

        public Filter() {
            FilterUtil.CreateAll( this, out all );
        }

        public void Assign( int pj, bool condition, List<byte> la, List<byte> lb ) {
            var l = condition ? la : lb;
            l.Add( ( byte )pj );
        }

        public void Clear() {
            foreach ( var l in all ) {
                l.Clear();
            }
        }
    }
}


} // namespace
