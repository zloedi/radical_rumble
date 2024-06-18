using System;
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
    

using Cl = RR.Client;
using Trig = RR.Pawn.ClientTrigger;
using PDF = RR.Pawn.Def.Flags;

    
public static class ClientPlayUnity {

static bool _initialized;
static GameObject _dummy = new GameObject( "__DUMMY__" );

static int [] _animSource = new int[Pawn.defs.Count];
static GameObject [] _model = new GameObject[Pawn.defs.Count];

static Animo.Crossfade [] _crossfade = new Animo.Crossfade[Pawn.MAX_PAWN];
static Vector2 [] _velocity = new Vector2[Pawn.MAX_PAWN];
static Vector2 [] _forward = new Vector2[Pawn.MAX_PAWN];
// one shot animation currently played, as opposed to a loop
static byte [] _animOneShot = new byte[Pawn.MAX_PAWN];
// idles are special i.e. when in attack loop
static byte [] _animIdle = new byte[Pawn.MAX_PAWN];
// one shot animations scale, i.e. attacks may be shorter than the attack animations
static float [] _animOneShotSpeed = new float[Pawn.MAX_PAWN];

static Pawn _pawn => Cl.game.pawn;

public static void Tick() {
    IMMGO.Begin();

    if ( ! _initialized ) {
        Initialize();
        _initialized = true;
    }

    int clock = ( int )Cl.clock;
    int clockDelta = ( int )Cl.clockDelta;

    _pawn.UpdateFilters();

    foreach ( var z in _pawn.filter.no_garbage ) {

        // newly spawned
        if ( Cl.TrigIsOn( z, Trig.Spawn ) ) {
            _pawn.mvPos[z] = _pawn.mvEnd[z];
            _pawn.mvStart_ms[z] = clock;

            // lookat the the first enemy 
            var enemies = _pawn.filter.enemies[_pawn.team[z]];
            Vector2 lookat = enemies.Count > 0 ? _pawn.mvPos[enemies[0]] : Vector2.zero;
            _forward[z] = lookat - _pawn.mvPos[z];

            _animOneShot[z] = 0;
            _animOneShotSpeed[z] = 1;

            _animIdle[z] = ( byte )_pawn.GetDef( z ).animIdle;
            Animo.ResetToState( _crossfade[z], _pawn.GetDef( z ).animIdle, offset: z * z * 2023 );
            Cl.Log( $"Spawned {_pawn.DN( z )}." ); 
        }

        // program new movement segment
        if ( Cl.TrigIsOn( z, Trig.Move ) ) {
            _pawn.mvStart[z] = _pawn.mvPos[z];
            _pawn.mvStart_ms[z] = clock - clockDelta;
            // kinda redundant, since velocity > 0 will reset it, but do it anyway
            _animOneShot[z] = 0;
            _animIdle[z] = ( byte )_pawn.GetDef( z ).animIdle;
            //Cl.Log( $"Plan move for {_pawn.DN( z )}." ); 
        }
    }
        
    // === program attack animation === 

    foreach ( var z in _pawn.filter.no_garbage ) {
        if ( ! Cl.TrigIsOn( z, Trig.Attack ) ) {
            continue;
        }

        // not animated
        if ( _animSource[_pawn.def[z]] == 0 ) {
            continue;
        }

        // attack target is garbage
        if ( _pawn.focus[z] == 0 ) {
            continue;
        }

        // not attacking actually, the server set the timestamp to zero for some reason
        if ( _pawn.atkEnd_ms[z] == 0 ) {
            continue;
        }

        // loop in combat idle between attacks
        _animIdle[z] = ( byte )_pawn.GetDef( z ).animIdleCombat;

        int atkDuration = _pawn.atkEnd_ms[z] - clock;

        int animSrc = _animSource[_pawn.def[z]];
        int oneShot = _pawn.GetDef( z ).animAttack;
        int animDuration = Animo.sourcesList[animSrc].duration[oneShot];

        _animOneShot[z] = ( byte )oneShot;
        // shrink the animation if longer than the attack duration
        _animOneShotSpeed[z] = atkDuration > animDuration ? 1 : animDuration / ( float )atkDuration;

        //Cl.Log( "atk duration: " + animDuration );
        //Cl.Log( "atk anim speed: " + _animOneShotSpeed[z] );
    }

    foreach ( var z in _pawn.filter.structures ) {
        _pawn.mvPos[z] = _pawn.mvEnd[z];
        _pawn.mvStart_ms[z] = _pawn.mvEnd_ms[z] = clock;
    }

    // FIXME: movers have 'patrol' point (was 'focus' in gym)
    // FIXME: and are filtered accordingly
    foreach ( var z in _pawn.filter.no_structures ) {
        updatePos( z );
    }

    foreach ( var z in _pawn.filter.flying ) {
        updatePos( z );
    }

    foreach ( var z in _pawn.filter.structures ) {
        int def = _pawn.def[z];
        Vector2 posGame = _pawn.mvPos[z];
        Vector3 posWorld = new Vector3( posGame.x, 0, posGame.y );
        ImmObject imo = DrawModel( _model[def], posWorld, handle: ( def << 16 ) | z );
        if ( _animSource[def] > 0 ) {
            Animo.UpdateState( clockDelta, _animSource[def], _crossfade[z], 1 );
            Animo.SampleAnimations( _animSource[def], imo.go.GetComponent<Animator>(),
                                                                                    _crossfade[z] );
        }
    }

    foreach ( var z in _pawn.filter.no_structures ) {
        int zf = _pawn.focus[z];
        Vector2 posGame = _pawn.mvPos[z];
        Vector2 toEnd = _pawn.mvEnd[z] - _pawn.mvPos[z];
        Vector2 fwdGame = _pawn.mvStart_ms[z] == _pawn.mvEnd_ms[z]
                            ? _pawn.mvPos[zf] - posGame
                            : toEnd;
        fwdGame = ( fwdGame.normalized + _forward[z] * 20 ).normalized;
        _forward[z] = fwdGame;
        Vector3 posWorld = new Vector3( posGame.x, 0, posGame.y );
        Vector3 fwdWorld = new Vector3( fwdGame.x, 0, fwdGame.y );
        int def = _pawn.def[z];
        ImmObject imo = DrawModel( _model[def], posWorld, fwdWorld, handle: ( def << 16 ) | z );
        int animSrc = _animSource[def];
        if ( animSrc == 0 ) {
            continue;
        }

        bool isMoving = _velocity[z].sqrMagnitude > 0.01f;

        // interrupt any single-shot animations if moving; start looping movement
        int oneShot = isMoving ? 0 : _animOneShot[z];

        // if not moving, the special idle could be used
        int loop = isMoving ? _pawn.GetDef( z ).animMove : _animIdle[z];

        if ( oneShot != 0 ) {
            if ( Animo.UpdateState( clockDelta, animSrc, _crossfade[z], oneShot, clamp: true,
                                                transition: 100, speed: _animOneShotSpeed[z] ) ) {
                _animOneShot[z] = 0;
            }
        } else {
            Animo.UpdateState( clockDelta, animSrc, _crossfade[z], loop, transition: 100 );
        }

        Animo.SampleAnimations( animSrc, imo.go.GetComponent<Animator>(), _crossfade[z] );
    }

    IMMGO.End();

    // === routines below ===

    void updatePos( int z ) {
        // zero delta move means stop
        // FIXME: remove if the pawn state is sent over the network
        if ( _pawn.mvEnd_ms[z] <= Cl.serverClock && _pawn.mvStart_ms[z] != _pawn.mvEnd_ms[z] ) {
            // FIXME: should lerp to actual end pos if offshoot
            _pawn.mvStart_ms[z] = _pawn.mvEnd_ms[z] = clock;
            return;
        }

        var prev = _pawn.mvPos[z];

        // the unity clock here is used just to extrapolate (move in the same direction)
        _pawn.MvLerpClient( z, clock, Time.deltaTime );

        _velocity[z] = clockDelta > 0
                                    ? ( _pawn.mvPos[z] - prev ) / ( clockDelta / 1000f )
                                    : Vector2.zero;
    }
}

static void StressTest() {
    Pawn.FindDefIdxByName( "Archer", out int def );
    for ( int i = 0; i < Pawn.MAX_PAWN; i++ ) {
        int x = i % 16;
        int y = i / 16;
        ImmObject imo = DrawModel( _model[def], new Vector3( x * 1.5f, 0, y * 1.5f ), handle: i );
        Animo.UpdateState( Cl.clockDelta, _animSource[def], _crossfade[i], 2 );
        Animo.SampleAnimations( _animSource[def], imo.go.GetComponent<Animator>(), _crossfade[i] );
    }
}

static ImmObject DrawModel( GameObject model, Vector3 pos, Vector3? forward = null,
                                                                float scale = -1, int handle = 0 ) {
    ImmObject imo = IMMGO.RegisterPrefab( model, handle: handle );
    imo.go.transform.position = pos;
    if ( forward != null && forward.Value.sqrMagnitude > 0.0001f ) {
        imo.go.transform.forward = forward.Value.normalized;
    }
    if ( scale != -1 ) {
        imo.go.transform.localScale = Vector3.one * scale;
    }
    return imo;
}

static void Initialize() {
    for ( int i = 0; i < _crossfade.Length; i++ ) {
        _crossfade[i] = new Animo.Crossfade();
    }

    Animo.Log = Cl.Log;
    Animo.Error = Cl.Error;

    for ( int i = 0; i < Pawn.defs.Count; i++ ) {
        var go = UnityLoad( $"mdl_{Cellophane.NormalizeName( Pawn.defs[i].name )}" ) as GameObject;
        if ( go ) {
            _model[i] = go;
            _animSource[i] = Animo.RegisterAnimationSource( go );
        } else {
            _model[i] = _dummy;
        }
    }
}

static UnityEngine.Object UnityLoad( string name ) {
    UnityEngine.Object result = Resources.Load( name );
    if ( ! result ) {
        Cl.Error( $"Failed to load '{name}'" );
        return null;
    }
    Cl.Log( $"Loaded '{name}'" );
    return result;
}


} }
