#if UNITY_STANDALONE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;

namespace RR {
    

using Cl = RR.Client;
using Trig = RR.Pawn.ClientTrigger;
using PDF = RR.Pawn.Def.Flags;

    
public static class ClientPlayUnity {

static float TestFloat_cvar = 0;

static bool _initialized;
static GameObject _dummy;
static GameObject _projectileFallback;

static int [] _animSource = new int[Pawn.defs.Count];
static GameObject [] _model = new GameObject[Pawn.defs.Count];
static GameObject [] _modelProjectilePrefab = new GameObject[Pawn.defs.Count];

static Animo.Crossfade [] _crossfade = new Animo.Crossfade[Pawn.MAX_PAWN];
static Vector2 [] _forward = new Vector2[Pawn.MAX_PAWN];
// one shot animation currently played, as opposed to a loop
static byte [] _animOneShot = new byte[Pawn.MAX_PAWN];
// one shot animations scale, i.e. attacks may be shorter than the attack animations
static float [] _animOneShotSpeed = new float[Pawn.MAX_PAWN];
static Color [] _colEmissive = new Color[Pawn.MAX_PAWN];
static Transform [] _muzzle = new Transform[Pawn.MAX_PAWN];

static Pawn _pawn => Cl.game.pawn;

public static void Tick() {
    IMMGO.Begin();

    if ( ! _initialized ) {
        Initialize();
        _initialized = true;
    }

    _pawn.UpdateFilters();

    foreach ( var z in _pawn.filter.no_garbage ) {
        if ( Cl.TrigIsOn( z, Trig.Spawn ) ) {
            _pawn.mvPos[z] = _pawn.mvEnd[z];
            _pawn.mvStart_ms[z] = _pawn.mvEnd_ms[z];

            _animOneShot[z] = 0;
            _animOneShotSpeed[z] = 1;

            _colEmissive[z] = Color.black;

            Animo.ResetToState( _crossfade[z], _pawn.GetDef( z ).animIdle, offset: z * z * 2023 );
            Cl.Log( $"Created {_pawn.DN( z )}" ); 
        }
    }

    foreach ( var z in _pawn.filter.no_garbage ) {
        // fix the lookat in another pass
        if ( Cl.TrigIsOn( z, Trig.Spawn ) ) {
            // lookat the the first enemy 
            var enemies = _pawn.filter.enemies[_pawn.team[z]];
            Vector2 lookat = enemies.Count > 0 ? _pawn.mvPos[enemies[0]] : Vector2.zero;
            foreach ( var ze in enemies ) {
                if ( _pawn.IsWinObjective( ze ) ) {
                    lookat = _pawn.mvPos[ze];
                    break;
                }
            }
            _forward[z] = lookat - _pawn.mvPos[z];
        }
    }

    foreach ( var z in _pawn.filter.no_garbage ) {
        // program new movement segment
        if ( Cl.TrigIsOn( z, Trig.Move ) ) {
            _pawn.mvStart[z] = _pawn.mvPos[z];
            _pawn.mvStart_ms[z] = Cl.clock - Cl.clockDelta;
            // kinda redundant, since velocity > 0 will reset it, but do it anyway
            _animOneShot[z] = 0;
        }
    }
        
    // === program attack animation === 

    foreach ( var z in _pawn.filter.no_garbage ) {
        if ( ! IsAttackTrigger( z ) ) {
            continue;
        }

        // not animated
        if ( _animSource[_pawn.def[z]] == 0 ) {
            continue;
        }

        int atkDuration = _pawn.atkEnd_ms[z] - Cl.clock;

        int animSrc = _animSource[_pawn.def[z]];
        int oneShot = _pawn.GetDef( z ).animAttack;
        int animDuration = Animo.sourcesList[animSrc].duration[oneShot];

        _animOneShot[z] = ( byte )oneShot;

        // shrink the animation if longer than the attack duration
        _animOneShotSpeed[z] = atkDuration > animDuration
                                                ? 1
                                                : animDuration / ( float )atkDuration;

        // we need to force attack anim again, since Animo fails if one shots are set 
        // while transitioning...
        Animo.CrossfadeToState( _crossfade[z], oneShot );

        //Cl.Log( "atk duration: " + animDuration );
        //Cl.Log( "atk anim speed: " + _animOneShotSpeed[z] );
    }

    // === program melee units 'hurt' when weapon hit lands === 

    foreach ( var z in _pawn.filter.melee ) {
        if ( ! IsAttackTrigger( z ) ) {
            continue;
        }

        int zf = _pawn.focus[z];
        int start = Cl.clock;
        int end = _pawn.atkEnd_ms[z];

        int animSrc = _animSource[_pawn.def[z]];
        int unscaledDuration = Animo.sourcesList[animSrc].duration[_animOneShot[z]];
        float duration = unscaledDuration / _animOneShotSpeed[z];
        float moment = TestFloat_cvar > 0 ? TestFloat_cvar : _pawn.GetDef( z ).momentLandHit;
        int landHit = Cl.clock + ( int )( duration * moment );

        // clock the time until impact and trigger 'hurt'
        SingleShot.AddConditional( dt => {

            // impact moment -- programmed as an animation event
            // this doesn't match the damage moment on the server,
            // but its simpler and good enough I guess
            if ( landHit > ( int )Cl.clock ) {
                return true;
            }

            // notify the target it is hit
            Cl.TrigRaise( zf, Trig.HurtVisuals );

            // TODO: handle any vfx management on impact here

            return false;
        } );

    }

    // === program ranged units projectile and trigger 'hurt' on impact === 

    foreach ( var z in _pawn.filter.ranged ) {
        if ( ! IsAttackTrigger( z ) ) {
            continue;
        }

        int zf = _pawn.focus[z];
        int start = Cl.clock;
        int end = _pawn.atkEnd_ms[z];
        GameObject prj = null;

        // clock the time until projectile impact and trigger 'hurt'
        SingleShot.AddConditional( dt => {
            // impact moment -- when clock passes the attack end
            if ( end > ( int )Cl.clock ) {
                return true;
            }

            // notify the target it is hit
            Cl.TrigRaise( zf, Trig.HurtVisuals );

            // even units without shooting vfx should be able to do 'hurt'
            if ( ! prj ) {
                return false;
            }

            // hide the porjectile mesh, but not the trail
            //  FIXME: cache it
            Transform [] ts = prj.GetComponentsInChildren<Transform>();
            foreach ( Transform tt in ts ) {
                if ( tt.CompareTag( "HideOnImpact" ) ) {
                    tt.gameObject.SetActive( false );
                }
            }

            return false;
        } );

        GameObject prefab = _modelProjectilePrefab[_pawn.def[z]];

        if ( ! prefab ) {
            prefab = _projectileFallback;
            if ( ! prefab ) {
                Cl.Error( "No projectile prefab." );
                continue;
            }
        }

        prj = GameObject.Instantiate( prefab );

        Vector3 getA() {
            return _muzzle[z] ? _muzzle[z].position
                                : new Vector3( _pawn.mvPos[z].x, 1, _pawn.mvPos[z].y );
        }

        Vector3 getB() {
            return new Vector3( _pawn.mvPos[zf].x, 1, _pawn.mvPos[zf].y );
        }

        prj.transform.position = getA();

        int shoot = Mathf.Max( Cl.clock, end - ( _pawn.AttackTime( z ) - _pawn.LoadTime( z ) ) );

        Vector3 a = getA();
        Vector3 b = getB();

        // the projectile one-shot
        SingleShot.Add( dt => {
            int clk = ( int )Cl.clock;

            if ( shoot > clk ) {
                // not time to shoot yet
                if ( ! _pawn.IsGarbage( z ) ) {
                    a = getA();
                }
                return;
            }

            prj.SetActive( true );

            if ( ! _pawn.IsGarbage( zf ) ) {
                b = getB();
            }

            float t = ( clk - shoot ) / ( float )( end - shoot );

            Vector3 c = Vector3.Lerp( a, b, t );

            prj.transform.position = c;
            prj.transform.forward = ( b - a ).normalized;
        },
        done: () => {
            GameObject.Destroy( prj );
        }, duration: 5 );
    }

    foreach ( var z in _pawn.filter.structures ) {
        _pawn.mvPos[z] = _pawn.mvEnd[z];
        _pawn.mvStart_ms[z] = _pawn.mvEnd_ms[z];
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
        Vector2 posGame = _pawn.mvPos[z];
        Vector3 posWorld = new Vector3( posGame.x, 0, posGame.y );

        string [] lookup = { "vfx_muzzle", "vfx_attack_rotor" };
        int def = _pawn.def[z];
        ImmObject imo = DrawModel( _model[def], posWorld, lookup: lookup,
                                                                    handle: ( def << 16 ) | z );
        _muzzle[z] = imo.GetRef( 0, 0 );
        if ( imo.GetRefs( 1, out List<Transform> rotors ) ) {
            int zf = _pawn.focus[z];
            foreach ( var rotor in rotors ) {
                if ( Cl.TrigIsOn( z, Trig.Spawn ) ) {
                    rotor.forward = new Vector3( _forward[z].x, 0, _forward[z].y ).normalized;
                } else if ( zf > 0 ) {
                    Vector2 posGameRotor = new Vector2( rotor.position.x, rotor.position.z );
                    Vector2 fwdOld = new Vector2( rotor.forward.x, rotor.forward.z );
                    Vector2 fwdGameRotor = _pawn.mvPos[zf] - posGameRotor;
                    fwdGameRotor = fwdGameRotor.normalized * 7.5f * Cl.clockDeltaSec + fwdOld;
                    fwdGameRotor = fwdGameRotor.normalized;
                    rotor.forward = new Vector3( fwdGameRotor.x, 0, fwdGameRotor.y );
                }
            }
        }

        updateEmissive( z, imo );

        if ( _animSource[def] > 0 ) {
            Animo.UpdateState( Cl.clockDelta, _animSource[def], _crossfade[z], 1 );
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
        fwdGame = ( fwdGame.normalized * 4 * Cl.clockDeltaSec + _forward[z] ).normalized;
        _forward[z] = fwdGame;
        Vector3 posWorld = new Vector3( posGame.x, 0, posGame.y );
        Vector3 fwdWorld = new Vector3( fwdGame.x, 0, fwdGame.y );
        int def = _pawn.def[z];
        string [] lookup = { "vfx_muzzle" };
        ImmObject imo = DrawModel( _model[def], posWorld, fwdWorld, lookup: lookup,
                                                                        handle: ( def << 16 ) | z );
        _muzzle[z] = imo.GetRef( 0, 0 );

        updateEmissive( z, imo );

        int animSrc = _animSource[def];
        if ( animSrc == 0 ) {
            continue;
        }

        bool isMoving = _pawn.IsMovingOnClient( z );

        // interrupt any single-shot animations if moving; start looping movement
        int oneShot = isMoving ? 0 : _animOneShot[z];

        // if not moving, the special idle could be used
        int loop = isMoving ? _pawn.GetDef( z ).animMove : _pawn.GetDef( z ).animIdle;

        if ( oneShot != 0 ) {
            if ( Animo.UpdateState( Cl.clockDelta, animSrc, _crossfade[z], oneShot, clamp: true,
                                                transition: 100, speed: _animOneShotSpeed[z] ) ) {
                _animOneShot[z] = 0;
            }
        } else {
            Animo.UpdateState( Cl.clockDelta, animSrc, _crossfade[z], loop, transition: 100 );
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
            _pawn.mvStart_ms[z] = _pawn.mvEnd_ms[z] = Cl.clock;
            return;
        }

        var prev = _pawn.mvPos[z];

        // the unity clock here is used just to extrapolate (move in the same direction)
        _pawn.MvLerpClient( z, Cl.clock, Time.deltaTime );
    }

    void updateEmissive( int z, ImmObject imo  ) {
        if ( Cl.TrigIsOn( z, Trig.HurtVisuals ) ) {
            _colEmissive[z] = new Color( 1, 0.8f, 0.8f );
        }

        if ( Cl.TrigIsOn( z, Trig.Spawn ) || _colEmissive[z].r > 0 ) {
            foreach ( var m in imo.mats ) {
                m.SetColor( "_EmissionColor", _colEmissive[z] );
                m.EnableKeyword( "_EMISSION" );
            }
            _colEmissive[z].r -= 3 * Cl.clockDeltaSec;
            _colEmissive[z].g -= 3 * Cl.clockDeltaSec;
            _colEmissive[z].b -= 3 * Cl.clockDeltaSec;
        }
    }
}

static ImmObject DrawModel( GameObject model, Vector3 pos, Vector3? forward = null,
                                    float scale = -1, string [] lookup = null, int handle = 0 ) {
    ImmObject imo = IMMGO.RegisterPrefab( model, garbageMaterials: true, lookupChildren: lookup,
                                                                                handle: handle );
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

    _dummy = new GameObject( "__DUMMY__" );

    for ( int i = 0; i < Pawn.defs.Count; i++ ) {
        var go = UnityLoad( $"mdl_{Cellophane.NormalizeName( Pawn.defs[i].name )}" ) as GameObject;
        if ( go ) {
            _model[i] = go;
            _animSource[i] = Animo.RegisterAnimationSource( go );
            if ( _animSource[i] > 0 ) {
                foreach ( var kv in Pawn.defs[i].anims ) {
                    int max = Animo.sourcesList[_animSource[i]].state.Count;
                    if ( kv.Value < 0 || kv.Value >= max ) {
                        Cl.Error( $"anim{kv.Key} has invalid Animo state: {kv.Value}, max: {max - 1}" );
                        _animSource[i] = 0;
                        break;
                    }
                }
            }
        } else {
            _model[i] = _dummy;
        }
    }

    for ( int i = 0; i < Pawn.defs.Count; i++ ) {
        var mdl = _model[i];
        Transform [] ts = mdl.GetComponentsInChildren<Transform>( includeInactive: true );
        foreach ( var t in ts ) {
            var nm = t.name.ToLowerInvariant();
            if ( nm.Contains( "vfx_projectile" ) ) {
                _modelProjectilePrefab[i] = t.gameObject;
                _modelProjectilePrefab[i].SetActive( false );
            }
        }
    }

    _projectileFallback = UnityLoad( "vfx_projectile_fallback" ) as GameObject;
    if ( ! _projectileFallback ) {
        _projectileFallback = _dummy;
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

static bool IsAttackTrigger( int z ) {
    if ( ! Cl.TrigIsOn( z, Trig.Attack ) ) {
        return false;
    }

    // attack target is garbage
    if ( _pawn.focus[z] == 0 ) {
        return false;
    }

    // not attacking actually, the server set the timestamp to zero for some reason
    if ( _pawn.atkEnd_ms[z] == 0 ) {
        return false;
    }

    return true;
}


} }

#else

public static class ClientPlayUnity {

public static void Tick() {
    RR.Client.Log( "RUNNING THE UNITY PLAYER STUB..." );
}

}

#endif
