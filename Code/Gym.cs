using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;

using Cl = RRClient;

static class Gym {

static int GymState_kvar = 0;

static string [] _tickNames;
static Action [] _ticks = TickUtil.RegisterTicks( typeof( Gym ), out _tickNames,
    Steer_tck
);

static string _stateName => _tickNames[GymState_kvar % _ticks.Length];

public static void Tick() {
    Draw.FillScreen();

    int t = GymState_kvar % _ticks.Length;
    Cl.TickKeybinds( context: $"gym_{_tickNames[t]}" );
    _ticks[t]();

    var wbox = Draw.wboxScreen.BottomCenter( Draw.wboxScreen.W, 20 * Draw.pixelSize );
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

// == STEERING TESTS == 

static Vector2 [] _strCircle = new Vector2[14];
static List<int> _strColliding = new List<int>();

class Steer {
    public Vector2 origin = Vector2.zero;
    public Vector2 target = Vector2.zero;
    public float radius = 0;
    public byte team = 0;
    public int chase = 0;
    public int lump = 0;
    public int lnext = 0;
    public int lprev = 0;
}

static List<Steer> _str = new List<Steer>() { new Steer() };

static void Steer_tck() {
    void drawCircle( Vector2 p, float r, Color c ) {
        int max = _strCircle.Length;
        float step = ( float )( Math.PI * 2f / max );
        Vector2 origin = Draw.GTS( p );
        float pxr = r * Draw.hexPixelSize;
        for ( int i = 0; i < max; i++ ) {
            Vector2 v = new Vector2( Mathf.Cos( i * step ), Mathf.Sin( i * step ) );
            _strCircle[i] = v * pxr + origin;
        }
        QGL.LateDrawLineLoop( _strCircle, color: c );
    }

    void draw( int n ) {
        var c = _str[n].team == 0 ? Color.cyan : Color.red;
        drawCircle( _str[n].origin, _str[n].radius, c );
    }

    int pickChase( int n ) {
        int furthest = -1;
        float max = 0;
        for ( int i = 0; i < _str.Count; i++ ) {
            float d = ( _str[i].origin - _str[n].origin ).sqrMagnitude;
            if ( _str[i].team == 1 && max < d ) {
                furthest = i;
                max = d;
            }
        }
        return furthest;
    }

    void simulate( int n ) {
        if ( _str[n].team != 0 ) {
            return;
        }

        if ( _str[n].chase == 0 ) {
            _str[n].chase = pickChase( n );

            if ( _str[n].chase == 0 ) {
                return;
            }

            Cl.Log( $"Picked to chase {_str[n].chase}" );
            _str[n].target = _str[_str[n].chase].origin;
            Cl.Log( $"Picked target at {_str[n].target}" );
        }

        Vector2 ab = _str[_str[n].chase].origin - _str[n].origin;

        float sq = ab.sqrMagnitude;
        if ( sq < 0.05f ) {
            _str[n].chase = 0;
            return;
        }

        Vector2 dir = ab / Mathf.Sqrt( sq );
        Vector2 feeler = _str[n].origin + dir * ( 1 - 0.1f );

        var col = Color.green; col.a = 0.3f;
        drawCircle( feeler, 1, col );
        
        _str[n].origin += ( _str[n].target - _str[n].origin ).normalized * Time.deltaTime;
    }

    for ( int i = 1; i < _str.Count; i++ ) {
        simulate( i );
    }

    for ( int i = 1; i < _str.Count; i++ ) {
        draw( i );
    }

    for ( int i = 1; i < _str.Count; i++ ) {
        int lump = _str[i].lump;
        if ( lump != 0 ) {
            var pos = Draw.GTS( _str[i].origin );
            QGL.LatePrint( _str[i].lump, pos, color: Color.yellow );
        }
    }
}

static void GymSteerPlace_kmd( string [] argv ) {
    if ( argv.Length < 2 ) {
        Cl.Log( $"{argv[0]} <0-3>" );
        return;
    }

    void place( float radius, int team ) {
        var s = new Steer();
        s.target = s.origin = Cl.mousePosGame;
        s.radius = radius;
        s.team = ( byte )team;
        int n = _str.Count;
        _str.Add( s );

        if ( team == 0 ) {
            return;
        }

        s.lprev = s.lnext = n;
        s.lump = n;

        _strColliding.Clear();
        for ( int i = 1; i < n; i++ ) {
            if ( _str[i].team == 0 ) {
                continue;
            }
            float r = _str[i].radius + _str[n].radius + 0.3f;
            if ( ( _str[i].origin - _str[n].origin ).sqrMagnitude < r * r ) {
                _strColliding.Add( i );
            }
        }

        if ( _strColliding.Count == 0 ) {
            return;
        }

        // merge colliding lumps into one

        for ( int c = 0; c < _strColliding.Count - 1; c++ ) {
            int c0 = _strColliding[c + 0];
            int c1 = _strColliding[c + 1];

            if ( _str[c0].lump == _str[c1].lump ) {
                continue;
            }

            int n0 = c0;
            int n1 = _str[c0].lnext;

            int n2 = c1;
            int n3 = _str[c1].lnext;

            _str[n0].lnext = n3;
            _str[n2].lnext = n1;

            _str[n3].lprev = n0;
            _str[n1].lprev = n2;
        }

        // set lump id

#if false
        int next = _strColliding[0];
        for ( int i = 0; ; i++ ) {

            _str[next].lump = s.lump;
            next = _str[next].lnext;

            if ( next == _strColliding[0] ) {
                break;
            }

            if ( i == 1000 ) {
                Cl.Error( "Infinite loop while setting lump." );
                break;
            }
        }
#else
        int next = _strColliding[0];
        do {
            _str[next].lump = s.lump;
            next = _str[next].lnext;
        } while ( next != _strColliding[0] );
#endif

        // insert the new node into the merged lump

        int ins = _strColliding[0];
        _str[n].lnext = _str[ins].lnext;
        _str[n].lprev = ins;
        _str[_str[n].lnext].lprev = n;
        _str[_str[n].lprev].lnext = n;
    }

    switch ( argv[1] ) {
        case "0": place( 0.6f, 1 ); break;
        case "1": place( 0.25f, 0 ); break;
        case "2": place( 0.3f, 0 ); break;
        case "3": place( 0.5f, 0 ); break;
        default: break;
    }

    Cl.Log( $"Placed {argv[1]}" );
}

static void GymSteerKill_kmd( string [] argv ) {
    int rem = 0;
    for ( int i = 1; i < _str.Count; i++ ) {
        var o = _str[i].origin;
        if ( ( o - Cl.mousePosGame ).sqrMagnitude <= 0.25f ) {
            rem = i;
            break;
        }
    }

    if ( rem == 0 ) {
        return;
    }

    if ( _str[rem].team == 0 ) {
        var o = _str[rem].origin;
        _str.RemoveAt( rem );
        Cl.Log( $"Removed pawn at {o.x} {o.y}" );
        return;
    }
}

static void GymSetState_kmd( string [] argv ) {
    TickUtil.SetState( argv, _ticks, _tickNames, ref GymState_kvar );
}

}
