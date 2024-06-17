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
        if ( Cl.TrigIsOn( z, Trig.Spawn ) ) {
            _pawn.mvPos[z] = _pawn.mvEnd[z];
            _pawn.mvStart_ms[z] = clock;
            Cl.Log( $"Spawned {_pawn.DN( z )}." ); 
        }

        if ( Cl.TrigIsOn( z, Trig.Move ) ) {
            // new movement segment arrives, plan movement on the client
            _pawn.mvStart[z] = _pawn.mvPos[z];
            _pawn.mvStart_ms[z] = clock - clockDelta;
            //Cl.Log( $"Plan move for {_pawn.DN( z )}." ); 
        }
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

    foreach ( var z in _pawn.filter.no_garbage ) {
        int zf = _pawn.focus[z];
        Vector2 posGame = _pawn.mvPos[z];
        Vector2 toEnd = _pawn.mvEnd[z] - _pawn.mvPos[z];
        Vector2 fwdGame = _pawn.mvStart_ms[z] == _pawn.mvEnd_ms[z]
                            ? _pawn.mvPos[zf] - posGame
                            : toEnd;
        Vector3 posWorld = new Vector3( posGame.x, 0, posGame.y );
        Vector3 fwdWorld = new Vector3( fwdGame.x, 0, fwdGame.y );
        int def = _pawn.def[z];
        ImmObject imo = DrawModel( _model[def], posWorld, fwdWorld, handle: ( def << 16 ) | z );
        if ( _animSource[def] > 0 ) {
            int state = _velocity[z].sqrMagnitude > 0.01f ? 2 : 1;
            Animo.UpdateState( clockDelta, _animSource[def], _crossfade[z], state );
            Animo.SampleAnimations( _animSource[def], imo.go.GetComponent<Animator>(),
                                                                                    _crossfade[z] );
        }
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
                                                                float scale = 1, int handle = 0 ) {
    ImmObject imo = IMMGO.RegisterPrefab( model, handle: handle );
    imo.go.transform.position = pos;
    if ( forward != null && forward.Value.sqrMagnitude > 0.0001f ) {
        imo.go.transform.forward = forward.Value.normalized;
    }
    imo.go.transform.localScale = Vector3.one * scale;
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

    for ( int i = 1; i < Pawn.MAX_PAWN; i++ ) {
        Animo.ResetToState( _crossfade[i], 2, offset: i * i * 2023 );
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
