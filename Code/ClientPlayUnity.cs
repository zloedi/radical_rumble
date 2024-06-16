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

public static void Tick() {
    if ( ! _initialized ) {
        Initialize();
        _initialized = true;
    }

    Pawn.FindDefIdxByName( "Archer", out int def );
    for ( int i = 0; i < Pawn.MAX_PAWN; i++ ) {
        int x = i % 16;
        int y = i / 16;
        ImmObject imo = DrawModel( _model[def], new Vector3( x * 1.5f, 0, y * 1.5f ), handle: i );
        Animo.UpdateState( Cl.clockDelta, _animSource[def], _crossfade[i], 2 );
        Animo.SampleAnimations( _animSource[def], imo.go.GetComponent<Animator>(), _crossfade[i] );
    }
}

static ImmObject DrawModel( GameObject model, Vector3 pos, float scale = 1, int handle = 0 ) {
    ImmObject imo = IMMGO.RegisterPrefab( model, handle: handle );
    imo.go.transform.position = pos;
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
