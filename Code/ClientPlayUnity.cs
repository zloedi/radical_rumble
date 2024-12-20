#if UNITY_STANDALONE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace RR {
    

using Cl = RR.Client;
using Trig = RR.Pawn.ClientTrigger;
using PDF = RR.Pawn.Def.Flags;

    
public static class ClientPlayUnity {

static float TestFloat_cvar = 0;

[Description( "Skip structures projectile visuals" )]
static bool ClSkipStructureProjectiles_kvar = false;
[Description( "Skip all projectile visuals" )]
static bool ClSkipProjectiles_kvar = false;
[Description( "Show the event 5 test UI" )]
static bool ClEvent5UI_kvar = false;
[Description( "Show the event 6 test UI" )]
static bool ClEvent6UI_kvar = false;

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
static Transform [] _bullseye = new Transform[Pawn.MAX_PAWN];
static Transform [] _emitterAttack = new Transform[Pawn.MAX_PAWN];
static float [] _emitterAttackDuration = new float[Pawn.MAX_PAWN];
static float [] _emitterAttackDelay = new float[Pawn.MAX_PAWN];

static Projectile _projectile = new Projectile();

static Player player => Cl.game.player;
static Pawn _pawn => Cl.game.pawn;

static Camera _camera;
static Vector2 _cameraMoveDir;
static float _cameraSpeed;
static Vector2 _cameraLookat;
static Vector3 _cameraOffset;

public static void Tick() {
    int localPlayer = 1; // player 0 is invalid, just to show the mana bar on observers
    int localTeam;
    float localMana;
    bool isObserver;

    isObserver = ! player.IsPlayer( Cl.zport );

    if ( ! isObserver ) {
        localPlayer = player.GetByZPort( Cl.zport );
    }

    localTeam = player.team[localPlayer];
    localMana = player.Mana( localPlayer, Cl.clock );

    GUIUnity.localTeam = localTeam;
    GUIUnity.localMana = localMana;
    GUIUnity.isObserver = isObserver;

    IMGO.Begin();

    if ( ! _initialized ) {
        Init();
        _initialized = true;
    }

    _pawn.UpdateFilters();
    _projectile.UpdateFilters();

    if ( _camera == null && _pawn.filter.objectives[localTeam].Count > 0 ) {
        int zLookat = _pawn.filter.objectives[localTeam][0];
        _camera = Camera.main;
        Plane p = new Plane( Vector3.up, Vector3.zero );
        Ray r = new Ray( _camera.transform.position, _camera.transform.forward );
        p.Raycast( r, out float d );
        _cameraOffset = _camera.transform.position - r.GetPoint( d );
        _cameraLookat = _pawn.mvEnd[zLookat];
        Cl.Log( $"Camera is looking at {_pawn.DN( zLookat )}; local team: {localTeam}" ); 
    }

    UpdateLookat();

    // === newly created pawns initialization ===

    foreach ( var z in _pawn.filter.no_garbage ) {
        if ( Cl.TrigIsOn( z, Trig.Spawn ) ) {
            _pawn.mvPos[z] = _pawn.mvEnd[z];
            _pawn.mvStart_ms[z] = _pawn.mvEnd_ms[z];

            _animOneShot[z] = 0;
            _animOneShotSpeed[z] = 1;

            Animo.ResetToState( _crossfade[z], _pawn.GetDef( z ).animIdle, offset: z * z * 2023 );
            Cl.Log( $"Created {_pawn.DN( z )}" ); 
        }
    }

    // fix the lookat in another pass, after all positions are settled
    foreach ( var z in _pawn.filter.no_garbage ) {
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
            Cl.Log( $"Fixed pawn lookat on {_pawn.DN( z )}" ); 
        }
    }

    // === death ===

    foreach ( var z in _pawn.filter.no_garbage ) {
        if ( Cl.TrigIsOn( z, Trig.Death ) ) {
            Vector2 posGame = _pawn.mvPos[z];
            Vector3 posWorld = new Vector3( posGame.x, 0, posGame.y );
            _pawn.mvEnd[z] = _pawn.mvPos[z];
            _pawn.mvStart_ms[z] = _pawn.mvEnd_ms[z];
            _animOneShot[z] = 0;
            _animOneShotSpeed[z] = 1;
            Cl.Log( $"{_pawn.DN( z )} dies." ); 
        }
    }

    // === program new movement segment ===

    foreach ( var z in _pawn.filter.no_garbage ) {
        if ( Cl.TrigIsOn( z, Trig.Move ) ) {
            _pawn.mvStart[z] = _pawn.mvPos[z];
            _pawn.mvStart_ms[z] = Cl.clock - Cl.clockDelta;
            // kinda redundant, since velocity > 0 will reset it, but do it anyway
            _animOneShot[z] = 0;
        }
    }
        
    // this just makes sure everyone is at the synced position on client reconnect
    foreach ( var z in _pawn.filter.no_garbage ) {
        if ( Cl.TrigIsOn( z, Trig.Attack ) ) {
            _pawn.mvPos[z] = _pawn.mvEnd[z];
            _pawn.mvStart_ms[z] = _pawn.mvEnd_ms[z];
        }
    }

    // === program attack animation === 

    foreach ( var z in _pawn.filter.no_garbage ) {
        if ( ! IsValidAttackTrigger( z ) ) {
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

        // we need to force attack anim again, since Animo fails if one-shots are set 
        // while transitioning...
        Animo.CrossfadeToState( _crossfade[z], oneShot );

        //Cl.Log( "atk duration: " + animDuration );
        //Cl.Log( "atk anim speed: " + _animOneShotSpeed[z] );
    }

    // === program melee units 'hurt' when weapon hit lands === 

    // assumes the attack one-shot is set
    foreach ( var z in _pawn.filter.melee ) {
        if ( ! IsValidAttackTrigger( z ) ) {
            continue;
        }

        int zf = _pawn.focus[z];
        int start = Cl.clock;
        int end = _pawn.atkEnd_ms[z];
        int landHit = GetLandHitMoment( z );

        // FIXME: we should use looping animation for the mellee attacks
        // FIXME: calculate the time in the loop, so the impact and land hit match
        // FIXME: and transition to that, fix Animo to be able to crossfade at specified time
        // clock the time until impact and trigger 'hurt'
        SingleShot.AddConditional( dt => {
            if ( end > Cl.clock ) {
                return true;
            }
            Cl.TrigRaise( zf, Trig.HurtVisuals );
            return false;
        } );

        // FIXME: just to make the dragon flame do burst, maybe use it on 'sprays' only, not 
        // FIXME: all attack emitters
        if ( _emitterAttack[z] ) {
            int outMoment = GetAttackOutMoment( z );
            float counter = 0;
            SingleShot.AddConditional( dt => {
                if ( landHit > Cl.clock ) {
                    return true;
                }
                if ( counter <= 0 ) {
                    Cl.TrigRaise( zf, Trig.HurtVisuals );
                    counter = 0.1f;
                }
                counter -= dt;
                return outMoment > Cl.clock;
            } );
        }
    }

    // === program ranged units projectile and trigger 'hurt' on impact === 

#if true // IMGO implementation

    Vector3 track( Transform [] t, int z ) {
        return t[z] ? t[z].position : new Vector3( _pawn.mvPos[z].x, 1, _pawn.mvPos[z].y );
    }

    foreach ( var z in _pawn.filter.ranged ) {

        if ( ! IsValidAttackTrigger( z ) ) {
            continue;
        }

        if ( ClSkipProjectiles_kvar
                || ( ClSkipStructureProjectiles_kvar && _pawn.IsStructure( z ) ) ) {
            continue;
        }

        int pj = _projectile.Create();
        if ( pj == 0 ) {
            Cl.Error( "Out of projectiles." );
            continue;
        }

        int zf = _pawn.focus[z];

        _projectile.zSrc[pj] = ( byte )z;
        _projectile.zDst[pj] = ( byte )zf;

        _projectile.posStart[pj] = track( _muzzle, _projectile.zSrc[pj] );
        _projectile.posEnd[pj] = track( _bullseye, _projectile.zDst[pj] );

        _projectile.msStart[pj] = GetLandHitMoment( z );
        _projectile.msEnd[pj] = _pawn.atkEnd_ms[z];
    }

    foreach ( var pj in _projectile.filter.no_garbage ) {
        if ( _pawn.IsGarbage( _projectile.zSrc[pj] )
                || _pawn.IsGarbage( _projectile.zDst[pj] ) ) {
            _projectile.StopTracking( pj );
        }
    }

    foreach ( var pj in _projectile.filter.travel ) {
        if ( _projectile.ShouldKeepTracking( pj ) ) {
            // track the target transform
            _projectile.posEnd[pj] = track( _bullseye, _projectile.zDst[pj] );

            // if the shoot moment is ahead in time, track the shooter transform
            if ( _projectile.msStart[pj] > Cl.clock ) {
                _projectile.posStart[pj] = track( _muzzle, _projectile.zSrc[pj] );
            }
        }
    }

    foreach ( var pj in _projectile.filter.travel ) {
        if ( ! _projectile.Lerp( pj, Cl.clock ) ) {
            // target reached, blink
            Cl.TrigRaise( _projectile.zDst[pj], Trig.HurtVisuals );
        }
    }

    foreach ( var pj in _projectile.filter.no_garbage ) {
        // the shoot moment is ahead in time, don't draw this projectile yet
        if ( _projectile.msStart[pj] > Cl.clock ) {
            continue;
        }

        int z = _projectile.zSrc[pj];

        var prefab = _modelProjectilePrefab[_pawn.def[z]];
        if ( ! prefab ) {
            prefab = _projectileFallback;
            if ( ! prefab ) {
                Cl.Error( "No projectile prefab." );
                continue;
            }
        }

        string [] lookup = { "vfx_hide_on_impact" };
        ImObject imo = IMGO.RegisterPrefab( prefab, lookupChildren: lookup,
                                                                handle: _projectile.id[pj] );
        Transform hideOnImpact = imo.GetRef( 0, 0 );
        if ( hideOnImpact && ! _projectile.IsTravelling( pj ) ) {
            hideOnImpact.gameObject.SetActive( false );
        }

        imo.go.transform.position = _projectile.posCur[pj];
        imo.go.transform.forward = _projectile.forward[pj].normalized;
    }

    foreach ( var pj in _projectile.filter.no_garbage ) {
        // kill off in a few seconds, so any trails die off
        if ( Cl.clock > _projectile.msEnd[pj] + 3000 ) {
            _projectile.Destroy( pj );
        }
    }

#else // SingleShot implementation

    foreach ( var z in _pawn.filter.ranged ) {
        if ( ! IsValidAttackTrigger( z ) ) {
            continue;
        }

        int zOriginalFocus = _pawn.focus[z];

        int start = Cl.clock;
        int end = _pawn.atkEnd_ms[z];
        GameObject prj = null;

        // clock the time until projectile impact and trigger 'hurt'
        SingleShot.AddConditional( dt => {
            // impact moment -- when clock passes the attack end
            if ( end > Cl.clock ) {
                return true;
            }

            if ( ! _pawn.IsGarbage( _pawn.focus[z] ) && _pawn.focus[z] == zOriginalFocus  ) {
                // no longer targeting same pawn, won't 'hurt' it
                Cl.TrigRaise( _pawn.focus[z], Trig.HurtVisuals );
            }

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
            int zf = _pawn.focus[z];
            return _bullseye[zf] ? _bullseye[zf].position
                                : new Vector3( _pawn.mvPos[zf].x, 1, _pawn.mvPos[zf].y );
        }

        prj.transform.position = getA();

        int shoot = Mathf.Max( Cl.clock, end - ( _pawn.AttackTime( z ) - _pawn.LoadTime( z ) ) );

        Vector3 a = getA();
        Vector3 b = getB();

        // the projectile one-shot
        SingleShot.Add( dt => {
            int clk = Cl.clock;

            if ( shoot > clk ) {
                // not time to shoot yet
                if ( ! _pawn.IsGarbage( z ) ) {
                    a = getA();
                }
                return;
            }

            prj.SetActive( true );

            // stop tracking the bullseye point if target has changed
            if ( ! _pawn.IsGarbage( _pawn.focus[z] ) && _pawn.focus[z] == zOriginalFocus  ) {
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
#endif

    // === some particle emitters are triggered on attack ===

    foreach ( var z in _pawn.filter.no_garbage ) {
        if ( ! IsValidAttackTrigger( z ) ) {
            continue;
        }

        ParticleSystem ps = _emitterAttack[z]?.GetComponent<ParticleSystem>();
        if ( ! ps ) {
            continue;
        }

        _emitterAttackDuration[z] = _emitterAttackDuration[z] != 0
                                                                ? _emitterAttackDuration[z]
                                                                : ps.main.duration;
        _emitterAttackDelay[z] = _emitterAttackDelay[z] != 0
                                                        ? _emitterAttackDelay[z]
                                                        : ps.main.startDelay.Evaluate( 0 );

        // need to stop to set durations (unity fu)
        ps.Stop( withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear );
        var main = ps.main;
        main.duration = _emitterAttackDuration[z] / _animOneShotSpeed[z];
        main.startDelay = _emitterAttackDelay[z] / _animOneShotSpeed[z];
        ps.Play();
    }

    foreach ( var z in _pawn.filter.structures ) {
        _pawn.mvPos[z] = _pawn.mvEnd[z];
        _pawn.mvStart_ms[z] = _pawn.mvEnd_ms[z];
    }

    // FIXME: movers have 'patrol' point (was 'focus' in gym)
    // FIXME: and are filtered accordingly
    foreach ( var z in _pawn.filter.no_structures ) {
        // zero delta move means stop
        // FIXME: remove if the pawn state is sent over the network
        if ( _pawn.mvEnd_ms[z] <= Cl.serverClock && _pawn.mvStart_ms[z] != _pawn.mvEnd_ms[z] ) {
            // FIXME: should lerp to actual end pos if offshoot
            _pawn.mvStart_ms[z] = _pawn.mvEnd_ms[z] = Cl.clock;
            continue;
        }

        var prev = _pawn.mvPos[z];

        // the unity clock here is used just to extrapolate (move in the same direction)
        _pawn.MvLerpClient( z, Cl.clock, Time.deltaTime );
    }

    foreach ( var z in _pawn.filter.structures ) {
        Vector2 posGame = _pawn.mvPos[z];
        Vector3 posWorld = new Vector3( posGame.x, 0, posGame.y );

        string [] lookup = { "vfx_muzzle", "vfx_bullseye", "vfx_emitter_attack",
                                                                            "vfx_attack_rotor" };
        ImObject imo = DrawPawn( z, posWorld, lookup: lookup );
        _muzzle[z] = imo.GetRef( 0, 0 );
        _bullseye[z] = imo.GetRef( 1, 0 );
        _emitterAttack[z] = imo.GetRef( 2, 0 );
        if ( imo.GetRefs( 3, out List<Transform> rotors ) ) {
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

        UpdateEmissive( z, imo );

        int def = _pawn.def[z];
        if ( _animSource[def] > 0 ) {
            Animo.UpdateState( Cl.clockDelta, _animSource[def], _crossfade[z], 1 );
            Animo.SampleAnimations( _animSource[def], imo.go.GetComponent<Animator>(),
                                                                                    _crossfade[z] );
        }
    }

    foreach ( var z in _pawn.filter.no_structures ) {
        // FIXME: what if we have dead that are not hp
        // FIXME: death animations/sequence
        if ( _pawn.hp[z] == 0 ) {
            continue;
        }

        bool isMoving = _pawn.IsMovingOnClient( z );
        Vector2 posGame = _pawn.mvPos[z];
        Vector2 toEnd = _pawn.mvEnd[z] - _pawn.mvPos[z];

        int zf = _pawn.focus[z];
        Vector2 fwdGame = isMoving ?  toEnd : _pawn.mvPos[zf] - posGame;
        fwdGame = ( fwdGame.normalized * 4 * Cl.clockDeltaSec + _forward[z] ).normalized;
        _forward[z] = fwdGame;

        Vector3 posWorld = new Vector3( posGame.x, 0, posGame.y );
        Vector3 fwdWorld = new Vector3( _forward[z].x, 0, _forward[z].y );

        string [] lookup = { "vfx_muzzle", "vfx_bullseye", "vfx_emitter_attack" };
        ImObject imo = DrawPawn( z, posWorld, fwdWorld, lookup: lookup );

        _muzzle[z] = imo.GetRef( 0, 0 );
        _bullseye[z] = imo.GetRef( 1, 0 );
        _emitterAttack[z] = imo.GetRef( 2, 0 );

        UpdateEmissive( z, imo );

        // animations sampling

        int animSrc = _animSource[_pawn.def[z]];
        if ( animSrc == 0 ) {
            continue;
        }

        // moving is with higher priority than one-shots, interrupt any one-shot here
        int oneShot = isMoving ? 0 : _animOneShot[z];

        // loop is either move or idle, attacks are kinda single-shot
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

    IMGO.End();

    // === GUI ===

    if ( ! QUI.canvas ) {
        QUI.canvas = new GameObject( "QUI Root" ).AddComponent<Canvas>();
        QUI.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }
    
    WrapBox.EnableCanvasScale();
    var wbScreen = new WrapBox( 0, 0, Screen.width, Screen.height );
    GUIUnity.DrawHealthBars();
    GUIUnity.DrawManaBar( wbScreen );
    if ( ClEvent5UI_kvar ) {
        GuiEvent5.Tick_ui( wbScreen );
    }
    if ( ClEvent6UI_kvar ) {
        GuiEvent6.Tick_ui( wbScreen );
    }
}

static void UpdateEmissive( int z, ImObject imo  ) {
    if ( Cl.TrigIsOn( z, Trig.Spawn ) ) {
        Cl.Log( $"Setting emissive to black on {_pawn.DN( z )}." );
        _colEmissive[z] = new Color( 0, 0, 0 );
        updateShader();
        return;
    }

    if ( Cl.TrigIsOn( z, Trig.HurtVisuals ) ) {
        _colEmissive[z] = new Color( 1, 0.9f, 0.8f );
    }

    if ( _colEmissive[z].r > 0 ) {
        updateShader();
        float dec = 2.75f * Cl.clockDeltaSec;
        _colEmissive[z].r = Mathf.Max( 0, _colEmissive[z].r - dec );
        _colEmissive[z].g = Mathf.Max( 0, _colEmissive[z].g - dec );
        _colEmissive[z].b = Mathf.Max( 0, _colEmissive[z].b - dec );
    }

    void updateShader() {
        foreach ( var m in imo.mats ) {
            m.SetColor( "_EmissionColor", _colEmissive[z] );
            m.EnableKeyword( "_EMISSION" );
        }
    }
}

static ImObject DrawPawn( int z, Vector3 pos, Vector3? forward = null, float scale = -1,
                                                        string [] lookup = null,
                                                        [CallerLineNumber] int lineNumber = 0,
                                                        [CallerMemberName] string caller = null ) {
    int def = _pawn.def[z];
    GameObject model = _model[def];
    ImObject imo = IMGO.RegisterPrefab( model, garbageMaterials: true, lookupChildren: lookup,
                                handle: ( def << 16 ) | z, lineNumber: lineNumber, caller: caller );
    imo.go.transform.position = pos;
    if ( forward != null && forward.Value.sqrMagnitude > 0.0001f ) {
        imo.go.transform.forward = forward.Value.normalized;
    }
    if ( scale != -1 ) {
        imo.go.transform.localScale = Vector3.one * scale;
    }
    return imo;
}

static void Init() {
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
                Cl.Log( $"Found vfx_projectile prefab on def '{Pawn.defs[i].name}:{i}'." );
            }
        }
    }

    _projectileFallback = UnityLoad( "vfx_projectile_fallback" ) as GameObject;
    if ( ! _projectileFallback ) {
        _projectileFallback = _dummy;
    }

    GUIUnity.Init();
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

static bool IsValidAttackTrigger( int z ) {
    if ( ! Cl.TrigIsOn( z, Trig.Attack ) ) {
        return false;
    }

    if ( _pawn.hp[z] == 0 ) {
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

// assumes the attack one-shot anim is already set
static int GetMoment( int z, float moment ) {
    int animSrc = _animSource[_pawn.def[z]];
    int unscaledDuration = Animo.sourcesList[animSrc].duration[_animOneShot[z]];
    float duration = unscaledDuration / _animOneShotSpeed[z];
    float momentLocal = TestFloat_cvar > 0 ? TestFloat_cvar : moment;
    int result = Cl.clock + ( int )( duration * momentLocal );
    return result;
}

static int GetLandHitMoment( int z ) {
    return GetMoment( z, _pawn.GetDef( z ).momentLandHit );
}

static int GetAttackOutMoment( int z ) {
    return GetMoment( z, _pawn.GetDef( z ).momentAttackOut );
}

static void UpdateLookat() {
    if ( ! _camera ) {
        return;
    }
    Vector3 lookat = new Vector3( _cameraLookat.x, 0, _cameraLookat.y );
    _camera.transform.position = lookat + _cameraOffset;
    _cameraLookat += CameraVelocity() * 10 * Time.deltaTime;
    _cameraSpeed -= 4 * Time.deltaTime;
}

static Vector2 CameraVelocity() {
    return Vector2.Lerp( _cameraMoveDir, Vector2.zero, 1 - _cameraSpeed );
}

static void UnityCameraUp_kmd( string [] argv ) {
    _cameraMoveDir = ( CameraVelocity() + Vector2.down ).normalized;
    _cameraSpeed = 1;
}

static void UnityCameraDown_kmd( string [] argv ) {
    _cameraMoveDir = ( CameraVelocity() + Vector2.up ).normalized;
    _cameraSpeed = 1;
}

static void UnityCameraLeft_kmd( string [] argv ) {
    _cameraMoveDir = ( CameraVelocity() + Vector2.right ).normalized;
    _cameraSpeed = 1;
}

static void UnityCameraRight_kmd( string [] argv ) {
    _cameraMoveDir = ( CameraVelocity() + Vector2.left ).normalized;
    _cameraSpeed = 1;
}


} }

#else

public static class ClientPlayUnity {

public static void Tick() {
    RR.Client.Log( "RUNNING THE UNITY PLAYER STUB..." );
}

}

#endif
