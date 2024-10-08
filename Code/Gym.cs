using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
using SDLPorts;
#endif

namespace RR {
    
    
using Cl = Client;
using Sv = Server;
using PS = Pawn.State;


static class Gym {


static int GymState_kvar = 0;

static string [] _tickNames;
static Action [] _ticks = TickUtil.RegisterTicksOfClass( typeof( Gym ), out _tickNames );

static string _stateName => _tickNames[GymState_kvar % _ticks.Length];

public static void Tick() {
    Draw.FillScreen();

    int t = GymState_kvar % _ticks.Length;
    Cl.TickKeybinds( context: $"gym_{_tickNames[t]}" );
    _ticks[t]();

    var wbox = Draw.wboxScreen.BottomCenter( Draw.wboxScreen.W, 20 );
    var text = $"Gym {_stateName}";
    WBUI.QGLTextOutlined( text, wbox, color: Color.white, fontSize: Draw.textSize );
}

public static void SolveOverlapping( List<Vector2> x, List<float> w, List<float> r,
                                                    int numSubsteps = 4,
                                                    float overshoot = 0.001f,
                                                    float eps = 0.0001f ) {
    float minrl = Mathf.Max( eps, overshoot * 0.1f );

    for ( int i = 0; i < numSubsteps; i++ ) {
        for ( int z1 = 0; z1 < x.Count; z1++ ) {
            for ( int z2 = 0; z2 < x.Count; z2++ ) {
                Vector2 x1 = x[z1];
                Vector2 x2 = x[z2];

                float w1 = w[z1];
                float w2 = w[z2];

                float r1 = r[z1];
                float r2 = r[z2];

                // the actual distance
                float l = ( x2 - x1 ).magnitude;

                // too far, don't bother
                if ( l - ( r1 + r2 ) > minrl ) {
                    continue;
                }

                if ( l < eps ) {
                    continue;
                }

                // desired (rest) distance, make sure we overshoot
                float l0 = r1 + r2 + overshoot;

                // inverted masses sum
                float sw = w1 + w2;
                if ( sw < eps ) {
                    continue;
                }

                // solve
                Vector2 s = ( l - l0 ) * ( x2 - x1 ) / l;
                Vector2 dx1 = +w1 / sw * s;
                Vector2 dx2 = -w2 / sw * s;
                x[z1] += dx1;
                x[z2] += dx2;
            }
        }
    }
}

// == RTS STILE MOVEMENT AND CONTROL == 

const int RTS_MAX_PAWN = 256;
static bool _rtsInitialized;
static int _rtsSel;
static int _rtsClock;
static List<byte> _lumpOpen = new List<byte>();
static List<byte> _lumpClosed = new List<byte>();
static List<byte> _rtsValid = new List<byte>();
static List<byte> _rtsMoving = new List<byte>();
static List<byte> _rtsNoMoving = new List<byte>();
static List<byte> [] _rtsLump = new List<byte>[RTS_MAX_PAWN];

struct RTSMove {
    public bool engaged => tb > ta;

    public int ta, tb;
    public Vector2 a, b;
    public Vector2 pos;

    // returns true while more to lerp
    public bool Lerp( int clock ) {
        int duration = tb - ta;

        if ( duration <= 0 ) {
            return false;
        }

        if ( clock >= tb ) {
            ta = tb;
            pos = a = b;
            return false;
        }

        int ti = clock - ta;
        float t = ( float )ti / duration;
        pos = Vector2.Lerp( a, b, t );

        return true;
    }

    public void Reset( Vector2 p, int clock ) {
        pos = a = b = p;
        ta = tb = clock;
    }

    public void Pivot( Vector2 newTarget, int speed, int clock ) {
        if ( speed == 0 ) {
            return;
        }

        a = pos;
        b = newTarget;

        float segmentDist = ( b - a ).magnitude;
        int duration = ( int )( 60 * 1000 * segmentDist ) / speed;

        ta = clock;
        tb = clock + duration;
    }
}

class RTSPawn {
    public byte [] def = null;
    public byte [] speed = null;
    public float [] radius = null;
    public byte [] lump = null;
    public RTSMove [] move = null;
    public Vector2 [] forward = null;
    public List<Array> allRows = new List<Array>();
}

static RTSPawn pawn = new RTSPawn();

static void RTS_Init() {
    ArrayUtil.CreateNulls( pawn, RTS_MAX_PAWN, out pawn.allRows );
    for ( int z = 0; z < RTS_MAX_PAWN; z++ ) { 
        _rtsLump[z] = new List<byte>();
    }
    for ( int z = 0; z < RTS_MAX_PAWN; z++ ) { 
        pawn.forward[z] = new Vector2( 1, 0 );
    }
}

static void RTS_tck() {
    if ( ! _rtsInitialized ) {
        RTS_Init();
        _rtsInitialized = true;
    }

    _rtsClock = ( int )( Time.unscaledTime * 1000 );

    _rtsValid.Clear();
    _rtsMoving.Clear();
    _rtsNoMoving.Clear();

    for ( int z = 0; z < RTS_MAX_PAWN; z++ ) { 
        if ( pawn.def[z] != 0 ) {
            _rtsValid.Add( ( byte )z );
        }
    }

    foreach ( var z in _rtsValid ) {
        var l = pawn.move[z].engaged ? _rtsMoving : _rtsNoMoving;
        l.Add( z );
    }

    foreach ( var z in _rtsValid ) {
        Vector2 forward = pawn.move[z].b - pawn.move[z].a;
        float sq = forward.sqrMagnitude;
        if ( sq > 0.00001f ) {
            pawn.forward[z] = forward / Mathf.Sqrt( sq );
        }
    }

    // generate chains of touching pawns -- lumps of pawns
    if ( _rtsNoMoving.Count >= 2 ) {
        _lumpOpen.Clear();
        _lumpClosed.Clear();

        _lumpOpen.AddRange( _rtsNoMoving );

        void close( int zA, int at ) {
            int zB = _lumpOpen[at];
            pawn.lump[zB] = pawn.lump[zA];
            _lumpClosed.Add( ( byte )zB );
            _lumpOpen.RemoveAt( at );
        }

        pawn.lump[_lumpOpen[0]] = _lumpOpen[0];
        close( _lumpOpen[0], 0 );

        int numChecks = 0;
        while ( true ) {

            for ( int i = _lumpOpen.Count - 1; i >= 0; i-- ) {
                int zA = _lumpClosed[0];
                int zB = _lumpOpen[i];

                float rA = pawn.radius[zA];
                float rB = pawn.radius[zB];

                float rAB = rA + rB;
                float sq = ( pawn.move[zA].pos - pawn.move[zB].pos ).sqrMagnitude;
                if ( sq <= rAB * rAB ) {
                    close( zA, i );
                }
                numChecks++;
            }

            if ( _lumpOpen.Count == 0 ) {
                break;
            }

            if ( _lumpClosed.Count > 0 ) {
                _lumpClosed.RemoveAt( 0 );
                if ( _lumpClosed.Count == 0 ) {
                    pawn.lump[_lumpOpen[0]] = _lumpOpen[0];
                    close( _lumpOpen[0], 0 );
                }
            }
        }

        //Qonsole.Log( $"numChecks: {numChecks}" );
    }

    // make it possible to lookup the lump from each pawn chained in it
    for ( int z = 0; z < RTS_MAX_PAWN; z++ ) { 
        _rtsLump[z].Clear();
    }
    foreach ( var z in _rtsNoMoving ) {
        _rtsLump[pawn.lump[z]].Add( z );
    }

    // look ahead for an obstructing lump
    List<byte> filterA = new List<byte>( _rtsMoving );
    List<byte> filterB = new List<byte>( _rtsNoMoving );
    filterA.Remove( ( byte )_rtsSel );
    filterB.Remove( ( byte )_rtsSel );
    filterA.Add( ( byte )_rtsSel );
    foreach ( var zA in filterA ) {
        Vector2 ahead = pawn.forward[zA] * pawn.radius[zA] * 2;
        Vector2 feeler = pawn.move[zA].pos + ahead;

        Draw.WireCircleGame( feeler, pawn.radius[zA], c: new Color( 1, 1, 1, 0.2f ) );

        foreach ( var zB in filterB ) {
            float rAB = pawn.radius[zA] + pawn.radius[zB];
            float rr = rAB * rAB;
            if ( ( feeler - pawn.move[zB].pos ).sqrMagnitude <= rr ) {
                List<byte> lump = _rtsLump[pawn.lump[zB]];
                int zMin = 0;
                int zMax = 0;
                float minAngle = 9999999;
                float maxAngle = -9999999;
                Vector2 start = new Vector2( 1, 0 );//pawn.move[zMin].pos - pawn.move[zA].pos;

                //QGL.LatePrint( "start", Draw.GTS( pawn.move[zMin].pos ) );

                foreach ( var zLump in lump ) {
                    Vector2 v = pawn.move[zLump].pos - pawn.move[zA].pos;
                    float angle = Vector2.SignedAngle( start, v );
                    //angle = angle < 0 ? 360f - angle : angle;
                    QGL.LatePrint( angle, Draw.GTS( pawn.move[zLump].pos ) );
                    if ( angle < minAngle ) {
                        zMin = zLump;
                        minAngle = angle;
                    }
                    if ( angle > maxAngle ) {
                        zMax = zLump;
                        maxAngle = angle;
                    }
                }

                Draw.SegmentGame( pawn.move[zA].pos, pawn.move[zMin].pos,
                                                                    c: new Color( 1, 1, 1, 0.2f ) );

                //Draw.SegmentGame( pawn.move[zA].pos, pawn.move[zMax].pos,
                //                                                  c: new Color( 1, 1, 1, 0.2f ) );

                //Draw.SegmentGame( feeler, pawn.move[zMax].pos, c: new Color( 1, 1, 1, 0.2f ) );

                //QGL.LatePrint( "min", Draw.GTS( pawn.move[zMin].pos ) );
                //QGL.LatePrint( "max", Draw.GTS( pawn.move[zMax].pos ) );
                break;
            }
        }
    }

    foreach ( var z in _rtsMoving ) {
        pawn.move[z].Lerp( _rtsClock );
    }

    foreach ( var z in _rtsValid ) {
        var c = z == _rtsSel ? Color.cyan : Color.red;

        if ( pawn.move[z].engaged ) {
            c.a = 0.2f;
            Draw.SegmentGame( pawn.move[z].a, pawn.move[z].b, c );
            Draw.WireCircleGame( pawn.move[z].b, pawn.radius[z], c );
        }

        c.a = 1;
        Draw.WireCircleGame( pawn.move[z].pos, pawn.radius[z], c );

        //QGL.LatePrint( pawn.lump[z], Draw.GTS( pawn.move[z].pos ), color: Color.yellow );
    }

    for ( int lump = 0; lump < RTS_MAX_PAWN; lump++ ) { 
        foreach ( var z in _rtsLump[lump] ) {
            if ( pawn.lump[z] != lump ) {
                QGL.LatePrint( $"{lump}/{pawn.lump[z]}", Draw.GTS( pawn.move[z].pos ), color: Color.red );
            }
        }
    }
}

static void RTS_Pick_kmd( string [] argv ) {
    foreach ( var z in _rtsValid ) {
        var rr = pawn.radius[z] * pawn.radius[z];
        if ( ( pawn.move[z].pos - Cl.mousePosGame ).sqrMagnitude <= rr ) {
            _rtsSel = z;
            break;
        }
    }
}

static void RTS_OrderMove_kmd( string [] argv ) {
    Cl.Log( "Move out..." );
    pawn.move[_rtsSel].Pivot( Cl.mousePosGame, pawn.speed[_rtsSel], _rtsClock );
}

static void RTS_Spawn_kmd( string [] argv ) {
    if ( argv.Length < 2 ) {
        Cl.Log( $"{argv[0]} <param>" );
        return;
    }

    if ( ! ArrayUtil.FindFreeColumn( pawn.def, out int z ) ) {
        Cl.Log( "Out of pawns." );
        return;
    }

    ArrayUtil.ClearColumn( pawn.allRows, z );

    pawn.def[z] = 1;
    switch ( argv[1] ) {
        case "1": pawn.radius[z] = 0.4f; break;
        case "2": pawn.radius[z] = 0.5f; break;
        case "3": pawn.radius[z] = 0.6f; break;
        default: break;
    }
    pawn.speed[z] = 120;
    pawn.move[z].Reset( Cl.mousePosGame, _rtsClock );

    _rtsSel = z;

    Cl.Log( "Spawned rts pawn." );
}

// == STEERING TESTS == 

//static Vector2 [] _strCircle = new Vector2[14];
//static List<int> _strColliding = new List<int>();
//
//class Steer {
//    public Vector2 origin = Vector2.zero;
//    public Vector2 target = Vector2.zero;
//    public float radius = 0;
//    public byte team = 0;
//    public int chase = 0;
//    public int lump = 0;
//    public int lnext = 0;
//    public int lprev = 0;
//}
//
//static List<Steer> _str = new List<Steer>() { new Steer() };
//
//static void Steer_tck() {
//    void draw( int n ) {
//        var c = _str[n].team == 0 ? Color.cyan : Color.red;
//        Draw.WireCircle( _str[n].origin, _str[n].radius, c );
//    }
//
//    int pickChase( int n ) {
//        int furthest = -1;
//        float max = 0;
//        for ( int i = 0; i < _str.Count; i++ ) {
//            float d = ( _str[i].origin - _str[n].origin ).sqrMagnitude;
//            if ( _str[i].team == 1 && max < d ) {
//                furthest = i;
//                max = d;
//            }
//        }
//        return furthest;
//    }
//
//    void simulate( int n ) {
//        if ( _str[n].team != 0 ) {
//            return;
//        }
//
//        if ( _str[n].chase == 0 ) {
//            _str[n].chase = pickChase( n );
//
//            if ( _str[n].chase == 0 ) {
//                return;
//            }
//
//            Cl.Log( $"Picked to chase {_str[n].chase}" );
//            _str[n].target = _str[_str[n].chase].origin;
//            Cl.Log( $"Picked target at {_str[n].target}" );
//        }
//
//        Vector2 ab = _str[_str[n].chase].origin - _str[n].origin;
//
//        float sq = ab.sqrMagnitude;
//        if ( sq < 0.05f ) {
//            _str[n].chase = 0;
//            return;
//        }
//
//        Vector2 dir = ab / Mathf.Sqrt( sq );
//        Vector2 feeler = _str[n].origin + dir * ( 1 - 0.1f );
//
//        var col = Color.green; col.a = 0.3f;
//        Draw.WireCircle( feeler, 1, col );
//        
//        _str[n].origin += ( _str[n].target - _str[n].origin ).normalized * Time.deltaTime;
//    }
//
//    for ( int i = 1; i < _str.Count; i++ ) {
//        simulate( i );
//    }
//
//    for ( int i = 1; i < _str.Count; i++ ) {
//        draw( i );
//    }
//
//    for ( int i = 1; i < _str.Count; i++ ) {
//        int lump = _str[i].lump;
//        var pos = Draw.GTS( _str[i].origin );
//#if false
//        if ( lump != 0 ) {
//            QGL.LatePrint( $"{i}-{lump}", pos, color: Color.yellow );
//        } else {
//            QGL.LatePrint( i, pos, color: Color.yellow );
//        }
//#else
//        if ( lump != 0 ) {
//            QGL.LatePrint( lump, pos, color: Color.yellow );
//        }
//#endif
//    }
//}
//
//static bool Collide( int a, int b ) {
//    float r = _str[a].radius + _str[b].radius;// + 0.3f;
//    if ( ( _str[a].origin - _str[b].origin ).sqrMagnitude < r * r ) {
//        return true;
//    }
//    return false;
//}
//
//static bool GetColliders( int n = 0 ) {
//    n = n != 0 ? n : _str.Count;
//    _strColliding.Clear();
//    for ( int i = 1; i < n; i++ ) {
//        if ( _str[i].team == 0 ) {
//            continue;
//        }
//        if ( Collide( i, n ) ) {
//            _strColliding.Add( i );
//        }
//    }
//    return _strColliding.Count > 0;
//}
//
//static void LumpRemove( int node ) {
//    int n = _str[node].lnext;
//    int p = _str[node].lprev;
//    _str[n].lprev = p;
//    _str[p].lnext = n;
//}
//
//static void LumpInsert( int node, int after ) {
//    _str[node].lnext = _str[after].lnext;
//    _str[node].lprev = after;
//    int n = _str[node].lnext;
//    int p = _str[node].lprev;
//    _str[n].lprev = node;
//    _str[p].lnext = node;
//}
//
//static void GymSteerPlace_kmd( string [] argv ) {
//    if ( argv.Length < 2 ) {
//        Cl.Log( $"{argv[0]} <0-3>" );
//        return;
//    }
//
//    void place( float radius, int team ) {
//        var s = new Steer();
//        s.target = s.origin = Cl.mousePosGame;
//        s.radius = radius;
//        s.team = ( byte )team;
//        int n = _str.Count;
//        _str.Add( s );
//
//        if ( team == 0 ) {
//            return;
//        }
//
//        s.lprev = s.lnext = n;
//        s.lump = n;
//
//        if ( ! GetColliders( n ) ) {
//            return;
//        }
//
//        // merge colliding lumps into one
//
//        for ( int c = 0; c < _strColliding.Count - 1; c++ ) {
//            int c0 = _strColliding[c + 0];
//            int c1 = _strColliding[c + 1];
//
//            if ( _str[c0].lump == _str[c1].lump ) {
//                continue;
//            }
//
//            if ( _str[c0].lnext == c0 && _str[c1].lnext == c1 ) {
//                if ( _str[c0].lprev != c0 ) {
//                    Cl.Error( "hoi 0" );
//                }
//                if ( _str[c1].lprev != c1 ) {
//                    Cl.Error( "hoi 1" );
//                }
//                _str[c0].lnext = c1;
//                _str[c0].lprev = c1;
//                _str[c1].lnext = c0;
//                _str[c1].lprev = c0;
//                Cl.Log( "[ffc000]both single[-]" );
//                continue;
//            }
//
//            if ( _str[c0].lnext == c0 ) {
//                if ( _str[c0].lprev != c0 ) {
//                    Cl.Error( "hoi 00" );
//                }
//                _str[_str[c1].lnext].lprev = c0;
//
//                _str[c0].lnext = _str[c1].lnext;
//                _str[c1].lnext = c0;
//
//                _str[c0].lprev = c1;
//                Cl.Log( "[ffc000]left single[-]" );
//                continue;
//            }
//
//            if ( _str[c1].lnext == c1 ) {
//                if ( _str[c1].lprev != c1 ) {
//                    Cl.Error( "hoi 11" );
//                }
//                _str[_str[c0].lnext].lprev = c1;
//
//                _str[c1].lnext = _str[c0].lnext;
//                _str[c0].lnext = c1;
//
//                _str[c1].lprev = c0;
//                Cl.Log( "[ffc000]right single[-]" );
//                continue;
//            }
//
//#if true
//            // if not stored like that
//            // infinite loop
//            int n0 = c0;
//            int n1 = _str[c0].lnext;
//
//            int n2 = c1;
//            int n3 = _str[c1].lnext;
//
//            _str[n0].lnext = n3;
//            _str[n2].lnext = n1;
//
//            _str[n3].lprev = n0;
//            _str[n1].lprev = n2;
//#else
//            int n0 = c0;
//            int n1 = _str[c0].lnext;
//
//            int n2 = c1;
//            int n3 = _str[c1].lnext;
//
//            _str[n0].lnext = _str[c1].lnext;
//            _str[n3].lprev = n0;
//
//            _str[n2].lnext = _str[c0].lnext;
//            _str[n1].lprev = n2;
//#endif
//        }
//
//        // set lump id
//
//#if false
//        int next = _strColliding[0];
//        for ( int i = 0; ; i++ ) {
//
//            _str[next].lump = s.lump;
//            next = _str[next].lnext;
//
//            if ( next == _strColliding[0] ) {
//                break;
//            }
//
//            if ( i == 1000 ) {
//                Cl.Error( "Infinite loop while setting lump." );
//                break;
//            }
//        }
//#else
//        int next = _strColliding[0];
//        do {
//            _str[next].lump = s.lump;
//            next = _str[next].lnext;
//        } while ( next != _strColliding[0] );
//#endif
//
//        // insert the new node into the merged lump
//
//        LumpInsert( n, after: _strColliding[0] );
//    }
//
//    switch ( argv[1] ) {
//        case "0": place( 0.6f, 1 ); break;
//        case "1": place( 0.25f, 0 ); break;
//        case "2": place( 0.3f, 0 ); break;
//        case "3": place( 0.5f, 0 ); break;
//        default: break;
//    }
//
//    Cl.Log( $"Placed {argv[1]}" );
//}
//
//static void LumpRemoveAndRewire( int rem ) {
///*
//    unchain rem 
//
//    set all nodes to lump id zero
//
//    find colliding with rem in lump
//
//    for each colliding with rem
//        make a chain of its own (new lump chain)
//
//
//    == for each new lump chain ==
//
//    for all (zero) lump nodes 
//
//        if next node is a non-zero-lump-id node (all nodes are non-zero/all are in a lump),
//        we are done rewiring the lump and splitting it into other lumps (go to quit)
//
//        for all nodes in new lump chain
//            if this zero-lump-id-node collides with this new-lump-node
//                set node lump id to new lump
//                move zero-lump-id node from zero lump to new lump chain
//                break
//*/
//    int lookup = _str[rem].lnext;
//
//    // set all nodes to lump id zero
//    for ( int n = lookup ; ; n = _str[n].lnext ) {
//        _str[n].lump = 0;
//        if ( _str[n].lnext == lookup ) {
//            break;
//        }
//    }
//
//    // unchain rem 
//    LumpRemove( rem );
//
//    // find colliding with rem in lump
//    _strColliding.Clear();
//    for ( int n = lookup ; ; n = _str[n].lnext ) {
//        if ( Collide( n, rem ) ) {
//            _strColliding.Add( n );
//        }
//        if ( _str[n].lnext == lookup ) {
//            break;
//        }
//    }
//
//    int numTests = 0;
//
//    // == for each new lump chain ==
//
//    foreach ( int cn in _strColliding ) {
//        if ( _str[cn].lump != 0 ) {
//            continue;
//        }
//
//        // get a nice node to start looking from
//        lookup = _str[cn].lnext;
//        if ( _str[lookup].lump != 0 ) {
//            Cl.Error( "This should never happen..." );
//            continue;
//        }
//
//        // make a chain of its own (new lump chain)
//        LumpRemove( cn );
//        _str[cn].lprev = _str[cn].lnext = cn;
//        _str[cn].lump = cn;
//
//        // for all (zero) lump nodes 
//        for ( int zn = lookup ; ;  ) {
//again:
//            // for all nodes in the new lump chain
//            for ( int n = cn ; ; n = _str[n].lnext ) {
//
//                numTests++;
//
//                // if this zero-lump-id-node collides with this new-lump-node
//                if ( Collide( zn, n ) ) {
//                    // move the zero-lump-id node from zero lump to new lump chain
//                    int next = _str[zn].lnext;
//                    LumpRemove( zn );
//                    LumpInsert( zn, after: n );
//
//                    // set zero node's lump id to new lump
//                    _str[zn].lump = cn;
//
//                    // out of zero lump id nodes (all nodes are non-zero/redistributed in lumps),
//                    // we are done rewiring the lump and splitting it into other lumps
//                    if ( next == zn ) {
//                        Cl.Log( $"Num tests: {numTests}" );
//                        goto quit;
//                    }
//
//                    // we found and moved a colliding node, retry with the rest
//                    // ignoring the loop check
//                    zn = lookup = next;
//                    goto again;
//                }
//
//                if ( _str[n].lnext == cn ) {
//                    break;
//                }
//            }
//
//            zn = _str[zn].lnext;
//            if ( zn == lookup ) {
//                break;
//            }
//        }
//    }
//quit:
//    ;
//}
//
//static void GymSteerKill_kmd( string [] argv ) {
//    int rem = 0;
//    for ( int i = 1; i < _str.Count; i++ ) {
//        Vector2 o = _str[i].origin;
//        if ( ( o - Cl.mousePosGame ).sqrMagnitude <= 0.25f ) {
//            rem = i;
//            break;
//        }
//    }
//
//    if ( rem == 0 ) {
//        return;
//    }
//
//    if ( _str[rem].team == 0 ) {
//        Vector2 o = _str[rem].origin;
//        _str.RemoveAt( rem );
//        Cl.Log( $"Removed pawn at {o.x} {o.y}" );
//        return;
//    }
//
//#if true
//    LumpRemoveAndRewire( rem );
//#else
//    int end = _str[rem].lnext;
//
//    // unchain rem
//    LumpRemove( rem );
//
//    {
//    _strColliding.Clear();
//    int n = end;
//    do {
//        if ( Collide( n, rem ) ) {
//            _strColliding.Add( n );
//        }
//        _str[n].lump = 0;
//        n = _str[n].lnext;
//    } while ( n != end );
//    }
//
//    //foreach ( int c in _strColliding ) {
//    {
//        int c = _strColliding[0];
//
//        //Cl.Log( $"c: {c}" );
//        end = _str[c].lnext;
//
//        LumpRemove( c );
//        _str[c].lprev = _str[c].lnext = c;
//        _str[c].lump = c;
//
//        int hoi = 0;
//        int nc = c;
//        do {
//            int hoi2 = 0;
//            bool collide = false;
//            for ( int n1 = end; ; hoi2++ ) {
//                //Cl.Log( $"nc: {nc} n1: {n1} end: {end}" );
//                //Cl.Log( $"testing {nc} vs {n1}" );
//
//                if ( _str[n1].lump != 0 ) {
//                    //Cl.Log( $"non null lump" );
//                    break;
//                }
//
//                if ( Collide( nc, n1 ) ) {
//                    //Cl.Log( $"collide {nc} {n1}, move {n1} to lump" );
//                    _str[n1].lump = c;
//                    if ( n1 == end ) {
//                        end = _str[n1].lnext;
//                    }
//                    LumpRemove( n1 );
//                    LumpInsert( n1, after: nc );
//                    nc = c;
//                    collide = true;
//                    break;
//                }
//
//                n1 = _str[n1].lnext;
//
//                if ( n1 == end ) {
//                    break;
//                }
//            }
//
//            //Cl.Log( $"inner loop end hoi2: {hoi2}" );
//
//            //Cl.Log( $"next nc: {_str[nc].lnext}" );
//            if ( ! collide ) {
//                nc = _str[nc].lnext;
//                if ( nc == c ) {
//                    break;
//                }
//            }
//
//            hoi++;
//        } while ( true );
//
//        if ( hoi == 1000 ) {
//            Cl.Error( "hoi" );
//        }
//
//        //nc = c;
//        //do {
//        //    _str[nc].lump = -1;
//        //    nc = _str[nc].lnext;
//        //} while ( nc != c );
//    }
//#endif
//
//    foreach ( var s in _str ) {
//        s.lprev = s.lprev > rem ? s.lprev - 1 : s.lprev;
//        s.lnext = s.lnext > rem ? s.lnext - 1 : s.lnext;
//        //s.lump = s.lump > rem ? s.lump - 1 : s.lump;
//    }
//
//    {
//    Vector2 o = _str[rem].origin;
//    _str.RemoveAt( rem );
//    Cl.Log( $"Removed pawn at {o.x} {o.y}" );
//    }
//}

static void GymSetState_kmd( string [] argv ) {
    TickUtil.SetState( argv, _ticks, _tickNames, ref GymState_kvar );
}



static Pawn svPawn => Sv.game.pawn;

// inverted mass -- either 0 (inert, infinite mass) or 1 (can be pushed aside)
static byte [] avdW = new byte[Pawn.MAX_PAWN]; 
// the point where this pawn is generally heading (while avoiding other pawns)
static Vector2 [] avdFocus = new Vector2[Pawn.MAX_PAWN]; 
// the enemy pawn this pawn is chasing
static byte [] avdChase = new byte[Pawn.MAX_PAWN]; 
// minimal pather waypoints to pawn reached while chasing a pawn
static int [] avdChaseMin = new int[Pawn.MAX_PAWN]; 
// the sphere ahead of this pawn used to avoid other pawns by 'steering'
static Vector2 [] avdFeeler = new Vector2[Pawn.MAX_PAWN]; 

struct Pair {
    public byte a;
    public byte b;
    public float sqDist;
}

// newly spawned
static List<byte> avdNew = new List<byte>();
static List<byte> no_avdNew = new List<byte>();
// pawns with a pawn to chase
static List<byte> avdChasing = new List<byte>();
static List<byte> no_avdChasing = new List<byte>();
// pawns with a point to chase
static List<byte> avdFocused = new List<byte>();
static List<byte> no_avdFocused = new List<byte>();
// currently in attack loop
static List<byte> avdAttacking = new List<byte>();
static List<byte> no_avdAttacking = new List<byte>();

// all possilble pairs of pawns of opposing teams ==
static List<Pair> avdPairEnemy = new List<Pair>();
// pairs of pawns with clipping feelers
static List<Pair> [] avdPairClip = new List<Pair>[2] { new List<Pair>(), new List<Pair>() };
// pawns who need their feelers separated by a distance constraint
static List<byte> [] avdCrntDist = new List<byte>[2] { new List<byte>(), new List<byte>() };
// pawns providing waypoints
static List<byte> [] avdWaypoints = new List<byte>[2] { new List<byte>(), new List<byte>() };
static List<byte> [] no_avdWaypoints = new List<byte>[2] { new List<byte>(), new List<byte>() };

/*
TODO:
* 1. Stop when in attack distance and become inert (w = 0)
* 2. Go around attacking teammates -- see todo.txt
. 3. Avoid non-passable terrain -- make the feelers of pawns on void terrain inert?
* 4. Split moving teammates 
?? 5. Split matching focus points for less traffic
*/
const float SEPARATE = 0.3f;
public static int TickServer() {
    svPawn.UpdateFilters();
    AvdFilter();

    // === solve clipping feelers, and keep them at desired ditance ===

    for ( int team = 0; team < 2; team++ ) {
        var clip = avdPairClip[team];
        const int numSubsteps = 3;
        for ( int i = 0; i < numSubsteps; i++ ) {

            // split any clipping feelers
            foreach ( var pr in clip ) {
                Vector2 x1 = avdFeeler[pr.a];
                Vector2 x2 = avdFeeler[pr.b];
                float w1 = avdW[pr.a];
                float w2 = avdW[pr.b];
                float r1 = svPawn.Radius( pr.a );
                float r2 = svPawn.Radius( pr.b );
                float l = ( x2 - x1 ).magnitude;
                if ( l - ( r1 + r2 ) > SEPARATE ) {
                    continue;
                }
                if ( l < 0.00001f ) {
                    continue;
                }
                float l0 = r1 + r2 + SEPARATE + 0.01f;
                float sw = w1 + w2;
                if ( sw < 0.00001f ) {
                    continue;
                }
                Vector2 s = ( l - l0 ) * ( x2 - x1 ) / l;
                Vector2 dx1 = +w1 / sw * s;
                Vector2 dx2 = -w2 / sw * s;
                avdFeeler[pr.a] += dx1;
                avdFeeler[pr.b] += dx2;
            }

            // keep the feeler at desired distance 
            foreach ( var z in avdCrntDist[team] ) {
                Vector2 x1 = svPawn.mvPos[z];
                Vector2 x2 = avdFeeler[z];
                float w1 = 0;
                float w2 = 1;
                float r1 = svPawn.Radius( z );
                float r2 = svPawn.Radius( z );
                float l = ( x2 - x1 ).magnitude;
                if ( l < 0.00001f ) {
                    // FIXME: ?!
                    continue;
                }
                float l0 = r1 * svPawn.SpeedSec( z );
                float sw = w1 + w2;
                Vector2 s = ( l - l0 ) * ( x2 - x1 ) / l;
                Vector2 dx1 = +w1 / sw * s;
                Vector2 dx2 = -w2 / sw * s;
                //svPawn.mvPos[z] += dx1;
                avdFeeler[z] += dx2;
            }
        }
    }

    foreach ( var z in svPawn.filter.no_garbage ) {
        SingleShot.Add( dt => {
            Draw.WireCircleGame( avdFeeler[z], svPawn.Radius( z ), Color.cyan );
            QGL.LatePrint( avdW[z], Draw.GTS( avdFeeler[z] ), color: Color.cyan );
        }, duration: 0.1f );
    }

    foreach ( var z in svPawn.filter.no_garbage ) {
        SingleShot.Add( dt => {
            Color c = Color.cyan; c.a = 0.4f;
            QGL.LateDrawLine( Draw.GTS( svPawn.mvPos[z] ), Draw.GTS( svPawn.mvPos[avdChase[z]] ),
                                                                                        color: c );
        }, duration: 0.1f );
    }

    // === latch pawns to chasing some proper enemy ===

    foreach ( var z in no_avdChasing ) {
        float minLen = 9999999;

        // use the waypoints of the opposing team (the towers are always waypoints)
        int team = ~svPawn.team[z] & 1;
        var zWPs = avdWaypoints[team];
        foreach ( var zWP in zWPs ) {
            int numWPs = Sv.game.GetCachedPathMvPosLen( z, zWP, out List<int> path, out float len );
            if ( numWPs <= 1 ) {
                continue;
            }
            if ( len < minLen ) {
                minLen = len;
                avdFocus[z] = Sv.game.HexToV( path[1] );
                avdChase[z] = zWP;
                avdChaseMin[z] = ( numWPs << 16 ) | 0xffff;
            }
        }
    }

    // === start chasing another pawn if better suited than currently chased one ===

    foreach ( var pr in avdPairEnemy ) {
        // FIXME: static float SvAggroRange_kvar = 6;
        bool canAggroA = ! IsAttacking( pr.a )
                            && svPawn.IsAttackPossible( pr.a, pr.b )
                            && ( svPawn.IsStructure( pr.a ) || pr.sqDist < 6 * 6 );
        bool canAggroB = ! IsAttacking( pr.b )
                            && svPawn.IsAttackPossible( pr.b, pr.a )
                            && ( svPawn.IsStructure( pr.b ) || pr.sqDist < 6 * 6 );

        if ( ! canAggroA && ! canAggroB ) {
            continue;
        }

        int pathCount = Sv.game.GetCachedPathMvPos( pr.a, pr.b, out List<int> pathAB );

        if ( pathCount != 2 ) {
            // no visibility between these two
            continue;
        }

        // num waypoints | distance between pawns, squared
        int score = ( 2 << 16 ) | ( int )( pr.sqDist * 100 );

        if ( canAggroA && avdChaseMin[pr.a] > score ) {
            avdChase[pr.a] = pr.b;
            avdChaseMin[pr.a] = score;
        }

        if ( canAggroB && avdChaseMin[pr.b] > score ) {
            avdChase[pr.b] = pr.a;
            avdChaseMin[pr.b] = score;
        }
    }

    // === correct the focus constantly, so teammates avoidance doesn't mess up pathing ===

    // if we don't correct the focus constantly,
    // clipping teammates eventually end up circling around the pather (focus) point,
    // not being able to reach it.
    // 'fix' the issue by making sure we always have an 'unreachable' pather waypoint ahead of us
    // FIXME: do it only for clipping feelers
    foreach ( var z in avdChasing ) {
        int zEnemy = avdChase[z];
        int numWPs = Sv.game.GetCachedPathMvPos( z, zEnemy, out List<int> path );

        if ( numWPs <= 1 ) {
            continue;
        }

        // FIXME: static float SvAggroRange_kvar = 6;
        float sq = svPawn.SqDist( z, avdChase[z] );
        if ( sq <= 6 * 6 ) {
            int score = ( numWPs << 16 ) | ( int )( sq * 100 );
            avdChaseMin[z] = Mathf.Min( avdChaseMin[z], score );
        }

        if ( numWPs == 2 ) {
            avdFocus[z] = svPawn.mvPos[zEnemy];
            continue;
        } 

        // focus on the next pather waypoint
        avdFocus[z] = Sv.game.HexToV( path[1] );
    }

    // === move the pawns before applying corrected feelers ===

    foreach ( var z in avdFocused ) {
        svPawn.MvChaseEndPoint( z, ZServer.clock );

        if ( svPawn.mvEnd[z] == avdFocus[z] ) {
            continue;
        }

        if ( IsAttacking( z ) ) {
            continue;
        }

        // these will get overwritten if the feelers got corrected (pawns feelers clipped)
        svPawn.mvEnd[z] = avdFocus[z];
        svPawn.mvEnd_ms[z] = ZServer.clock + MvDurationMs( z, svPawn.mvPos[z], svPawn.mvEnd[z] );
    }

    // === apply corrected feelers for clipping pawns ===

    for ( int team = 0; team < 2; team++ ) {
        var clip = avdPairClip[team];
        foreach ( var pr in clip ) {
            int [] za = { pr.a, pr.b };
            for ( int i = 0; i < 2; i++ ) {
                int z = za[i];

                // attacking units are also inert
                if ( avdW[z] == 0 ) {
                    continue;
                }

                svPawn.mvEnd[z] = avdFeeler[z];
                svPawn.mvEnd_ms[z] = ZServer.clock + MvDurationMs( z, svPawn.mvPos[z],
                                                                                svPawn.mvEnd[z] );
            }
        }
    }

    // === newly spawned transition to 'patrol' ====

    foreach ( var z in avdNew ) {
        svPawn.SetState( z, PS.Patrol );

        // FIXME: move to pawn create
        avdFocus[z] = Vector2.zero;
        avdChase[z] = 0;
        avdChaseMin[z] = 255 << 16;
        StopAttack( z );
    }

    // === pawn is in attack range to chased enemy, stop, make inert, trigger initial attack ====

    foreach ( var z in no_avdAttacking ) {
        int zAtk = z;
        int zDfn = avdChase[z];
        float dsq = svPawn.SqDist( zAtk, zDfn );
        float dneed = svPawn.DistanceForAttack( zAtk, zDfn );

        if ( dsq <= dneed * dneed ) {
            // stop
            svPawn.MvInterruptSoft( zAtk, ZServer.clock );

            // make inert so other pawns can't push us around
            avdW[zAtk] = 0;

            // start attacking
            // the initial attack loop is shorter
            // FIXME: maybe should be def thing
            svPawn.atkEnd_ms[zAtk] = ZServer.clock + svPawn.AttackTime( zAtk ) / 2;
            //Sv.Log( $"{svPawn.DN( zAtk )} starts attacking {svPawn.DN( zDfn )}" );
        }
    }

    // === attack loop: apply damage, reload another attack ====

    foreach ( var z in avdAttacking ) {
        // pawns in the list may die while walking the list
        if ( svPawn.IsDead( z ) ) {
            continue;
        }

        // waiting for impact
        if ( InAttackLoop( z ) ) {
            continue;
        }

        // damage application
        int zDfn = avdChase[z];
        svPawn.hp[zDfn] = ( ushort )Mathf.Max( 0, svPawn.hp[zDfn] - svPawn.Damage( z ) );
        //Sv.Log( $"{svPawn.DN( z )} attacks {svPawn.DN( zDfn )}" );

        // death
        if ( svPawn.hp[zDfn] == 0 ) {
            svPawn.SetState( zDfn, PS.Dead );

            StopAttack( zDfn );
            svPawn.MvSnapToEnd( zDfn );
            avdChase[zDfn] = 0;
            avdW[zDfn] = 0; 
            avdFocus[zDfn] = Vector2.zero; 
            avdChaseMin[zDfn] = 0; 
            avdFeeler[zDfn] = Vector2.zero; 

            continue;
        }

        // check if still in range
        int a = z;
        int b = avdChase[z];
        float dsq = svPawn.SqDist( a, b );
        float dneed = svPawn.DistanceForAttack( a, b );
        if ( dsq > dneed * dneed ) {
            StopAttack( z );
            continue;
        }

        // loop another attack if still in range
        int extra = ZServer.clock - svPawn.atkEnd_ms[z];
        svPawn.atkEnd_ms[z] = ZServer.clock + svPawn.AttackTime( z ) - extra;
    }

    // === handle dead pawns ===

    // keep disengage before the potential recycling of dead
    foreach ( var zd in svPawn.filter.ByState( PS.Dead ) ) {
        // remove engagement to dead
        foreach ( var z in svPawn.filter.no_garbage ) {
            if ( avdChase[z] == zd ) {
                //Sv.Log( $"Disengage {svPawn.DN( z )} from dead {svPawn.DN( zd )}" );
                StopAttack( z );
                avdChase[z] = 0;
            }
        }
    }

    // wait for any running attacks and recycle
    foreach ( var z in svPawn.filter.ByState( PS.Dead ) ) {
        // if dead and couldn't reload, destroy pawn, cancelling this attack
        int t = svPawn.AttackTime( z ) - svPawn.LoadTime( z );
        if ( svPawn.atkEnd_ms[z] - t > ZServer.clock ) {
            Sv.game.Destroy( z );
            continue;
        }

        // the dead pawns attacks may still connect, postpone garbage until attack end
        if ( svPawn.atkEnd_ms[z] <= ZServer.clock ) {
            Sv.game.Destroy( z );
        }
    }

    // copy move position to the synced fixed point transmit
    foreach ( var z in svPawn.filter.no_garbage ) {
        svPawn.mvEnd_tx[z] = Game.VToTx( svPawn.mvEnd[z] );
    }

    // FIXME: remove focus, use chase when moved to pawn
    // FIXME: just used for transmit
    foreach ( var z in svPawn.filter.no_garbage ) {
        svPawn.focus[z] = avdChase[z];
    }

#if false
    foreach ( var z in svPawn.filter.no_garbage ) {
        Vector2 d = Game.TxToV( svPawn.mvEnd_tx[z] ) - svPawn.mvEnd[z];
        if ( d.sqrMagnitude > 0.01f * 0.01f ) {
            Sv.Error( "Broken tx:"
                    + " " + svPawn.mvEnd[z]
                    + " " + Game.TxToV( svPawn.mvEnd_tx[z] )
                    + " " + svPawn.mvEnd_tx[z].ToString( "X" )
            );
        }
    }
#endif

    return 0;
}

static void PrintPairEnemy_cmd( string [] argv ) {
    foreach ( var pr in avdPairEnemy ) {
        float d = Mathf.Sqrt( pr.sqDist );
        Qonsole.Log( $"{pr.a} [{svPawn.team[pr.a]}] : {pr.b} [{svPawn.team[pr.b]}] -- {d}" );
    }
}

// FIXME: in Pawn.cs
static int MvDurationMs( int z, Vector2 a, Vector2 b ) {
    float sq = ( b - a ).sqrMagnitude;
    if ( sq < 0.000001f ) {
        return 0;
    }
    return ( int )( Mathf.Sqrt( sq ) / svPawn.SpeedSec( z ) * 1000 );
}

// point this pawn is generally advancing to
static bool HasFocusPos( int z ) {
    return ! svPawn.IsStructure( z ) && avdFocus[z] != Vector2.zero;
}

static bool IsChasingEnemy( int z ) {
    return avdChase[z] != 0;
}

static bool IsAttacking( int z ) {
    return svPawn.atkEnd_ms[z] != 0;
}

// FIXME: pass clock
static bool InAttackLoop( int z ) {
    return svPawn.atkEnd_ms[z] > ZServer.clock;
}

static void StopAttack( int z ) {
    // stop any attack loop
    svPawn.atkEnd_ms[z] = 0;
    // let other teammates push us around
    avdW[z] = ( byte )( svPawn.IsStructure( z ) ? 0 : 1 );
}

static int GetCachedPathMvPos( int zSrc, int zTarget, out List<int> path ) {
    return Sv.game.GetCachedPathMvPos( zSrc, zTarget, out path );
}

static int NearestAttackableEnemy( int z ) {
    var enemies = svPawn.filter.enemies[svPawn.team[z]];
    int zClose = 0;
    float min = 99999999;
    foreach ( var zE in enemies ) {
        float sq = ( svPawn.mvPos[zE] - svPawn.mvPos[z] ).sqrMagnitude;
        if ( sq < min ) {
            min = sq;
            zClose = zE;
        }
    }
    return zClose;
}

static void AvdFilter() {
    avdNew.Clear();
    no_avdNew.Clear();
    
    avdChasing.Clear();
    no_avdChasing.Clear();

    avdFocused.Clear();
    no_avdFocused.Clear();

    avdAttacking.Clear();
    no_avdAttacking.Clear();

    avdPairEnemy.Clear();

    for ( int team = 0; team < 2; team++ ) {
        avdPairClip[team].Clear();
        avdCrntDist[team].Clear();
        avdWaypoints[team].Clear();
        no_avdWaypoints[team].Clear();
    }

    // we need to let the client snap the position on spawn, do nothing for a tick
    foreach ( var z in svPawn.filter.no_garbage ) {
        assign( z, svPawn.GetState( z ) == PS.None, avdNew, no_avdNew );
    }

    foreach ( var z in no_avdNew ) {
        assign( z, IsChasingEnemy( z ), avdChasing, no_avdChasing );
    }

    // only pawns chasing other pawns can have focus
    foreach ( var z in avdChasing ) {
        assign( z, HasFocusPos( z ), avdFocused, no_avdFocused );
    }

    foreach ( var z in avdChasing ) {
        assign( z, IsAttacking( z ), avdAttacking, no_avdAttacking );
    }

    for ( int team = 0; team < 2; team++ ) {
        var zs = svPawn.filter.team[team];
        foreach ( var z in zs ) {
            assign( z, svPawn.IsPatrolWaypoint( z ), avdWaypoints[team], no_avdWaypoints[team] );
        }
    }

    // FIXME: not a filter
    foreach ( var z in svPawn.filter.no_garbage ) {
        avdFeeler[z] = svPawn.mvPos[z];

        // inert stuff have their feelers at their origins
        if ( avdW[z] == 0 ) {
            continue;
        }

        Vector2 toLerp = svPawn.mvEnd[z] - svPawn.mvPos[z];
        Vector2 toFocus = avdFocus[z] - svPawn.mvPos[z];
        if ( toLerp.sqrMagnitude < 0.00001f || toFocus.sqrMagnitude < 0.00001f ) {
            // FIXME: is there a situation where no more to lerp, but still have focus?
            continue;
        }

        // add weights here, if you want to change the behaviour
        Vector2 v = toLerp.normalized + toFocus.normalized;
        if ( v.sqrMagnitude < 0.00001f ) {
            continue;
        }

        avdFeeler[z] = svPawn.mvPos[z] + v.normalized * svPawn.Radius( z ) * svPawn.SpeedSec( z );
    }

    // === all possilble pairs of pawns on opposing teams ==

    {

    var l = svPawn.filter.no_garbage;
    for ( int i = 0; i < l.Count - 1; i++ ) {
        for ( int j = i + 1; j < l.Count; j++ ) {
            int zA = l[i];
            int zB = l[j];
            if ( svPawn.team[zA] != svPawn.team[zB] ) {
                avdPairEnemy.Add( new Pair {
                    a = ( byte )zA,
                    b = ( byte )zB,
                    sqDist = svPawn.SqDist( zA, zB ),
                } );
            }
        }
    }

    }

    // === pawns with clipping feelers on the same team ===

    for ( int team = 0; team < avdPairClip.Length; team++ ) {
        var clip = avdPairClip[team];
        var tl = svPawn.filter.team[team];

        for ( int i = 0; i < tl.Count - 1; i++ ) {
            for ( int j = i + 1; j < tl.Count; j++ ) {
                int zA = tl[i];
                int zB = tl[j];
                float r = svPawn.Radius( zA ) + svPawn.Radius( zB ) + SEPARATE;
                Vector2 d = avdFeeler[zB] - avdFeeler[zA];
                if ( d.sqrMagnitude <= r * r ) {
                    clip.Add( new Pair {
                        a = ( byte )zA,
                        b = ( byte )zB,
                        sqDist = svPawn.SqDist( zA, zB ),
                    } );
                }
            }
        }
    }

    // === moving pawns (w != 0) keep their feelers at a distance (distance constraint) ===

    for ( int team = 0; team < 2; team++ ) {
        var clip = avdPairClip[team];

        // keep feeler at desired distance
        foreach ( var pr in clip ) {
            if ( avdW[pr.a] != 0 ) {
                avdCrntDist[team].Add( pr.a );
            }
            if ( avdW[pr.b] != 0 ) {
                avdCrntDist[team].Add( pr.b );
            }
        }
    }
    
    void assign( int z, bool condition, List<byte> la, List<byte> lb ) {
        var l = condition ? la : lb;
        l.Add( ( byte )z );
    }
}


}


}
