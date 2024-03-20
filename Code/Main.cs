using System;
using System.ComponentModel;

#if UNITY_STANDALONE
using UnityEngine;
#else
using SDLPorts;
using GalliumMath;
#endif

using Cl = RRClient;
using Sv = RRServer;

#if SDL
static class SDLApp {
    static void Init() {
        Qonsole.Init();
        Qonsole.Start();
        Qonsole.Log( Guid.NewGuid() );
        // SDL ports
        Application.Log = s => Qonsole.Log( "Application: " + s );
        Application.Error = s => Qonsole.Error( "Application: " + s );
    }

    static void Tick() {
        Qonsole.HandleSDLMouseMotion( Input.mousePosition.x, Input.mousePosition.y );

        // will invoke our ticks on qonsole_tick
        Qonsole.Update();

        Qonsole.RenderGL();
    }

    static void Done() {
        Qonsole.OnApplicationQuit();
    }

    static void OnText( string txt ) {
        Qonsole.HandleSDLTextInput( txt );
    }

    static void OnKey( KeyCode kc ) {
        Qonsole.HandleSDLKeyDown( kc );
    }

    static void Main( string [] argv ) {
        Application.Init = Init;
        Application.Tick = Tick;
        Application.Done = Done;
        Application.OnText = OnText;
        Application.OnKey = OnKey;

        // main loop goes on
        Application.Run( argv );
    }
}
#endif

static class Main {

[Description( "Always run the local server no matter if target ip is localhost." )]
static bool LocalServerAlwaysOn_kvar = false;

[Description( "Frame duration in milliseconds." )]
static bool ShowFrameTime_kvar = false;

static bool _initialized = false;

static void QonsolePreConfig_kmd( string [] argv ) {
    // change this to wipe cfg to defautlts
    Cellophane.ConfigVersion_kvar = 4;

    Qonsole.TryExecute( @"
        bind Alpha1 ""cl_select_to_spawn Brute"" play;
        bind Alpha2 ""cl_select_to_spawn Archer"" play;
        bind Alpha3 ""cl_select_to_spawn Flyer"" play;

        //bind Alpha5 ""cl_forced_spawn Brute"" play
        //bind Alpha6 ""cl_forced_spawn Archer"" play
        //bind Alpha7 ""cl_forced_spawn Flyer"" play
        //bind Alpha8 ""cl_forced_spawn Brute 1"" play
        //bind Alpha9 ""cl_forced_spawn Archer 1"" play
        //bind Alpha0 ""cl_forced_spawn Flyer 1"" play

        bind K ""cl_kill"" play;

        bind Alpha1  ""ed_atk_pos_solver_place 1 0"" ed_atk_pos_solver;
        bind Alpha2  ""ed_atk_pos_solver_place 2 0"" ed_atk_pos_solver;
        bind Alpha3  ""ed_atk_pos_solver_place 3 0"" ed_atk_pos_solver;
        bind Alpha4  ""ed_atk_pos_solver_place 4 0"" ed_atk_pos_solver;
        bind Alpha5  ""ed_atk_pos_solver_place 5 0"" ed_atk_pos_solver;

        bind Alpha6 ""ed_atk_pos_solver_place 1 1"" ed_atk_pos_solver;
        bind Alpha7 ""ed_atk_pos_solver_place 2 1"" ed_atk_pos_solver;
        bind Alpha8 ""ed_atk_pos_solver_place 3 1"" ed_atk_pos_solver;
        bind Alpha9 ""ed_atk_pos_solver_place 4 1"" ed_atk_pos_solver;
        bind Alpha0 ""ed_atk_pos_solver_place 5 1"" ed_atk_pos_solver;

        bind K ""ed_atk_pos_solver_remove"" ed_atk_pos_solver;

        bind Alpha1 ""rts_spawn 1""    gym_rts;
        bind Alpha2 ""rts_spawn 2""    gym_rts;
        bind Alpha3 ""rts_spawn 3""    gym_rts;
        bind Mouse0 ""rts_pick""       gym_rts;
        bind Mouse1 ""rts_order_move"" gym_rts;

        //bind Alpha1  ""gym_steer_place 1"" gym_steer;
        //bind Alpha2  ""gym_steer_place 2"" gym_steer;
        //bind Alpha3  ""gym_steer_place 3"" gym_steer;
        //bind Alpha0  ""gym_steer_place 0"" gym_steer;
        //bind K       ""gym_steer_kill"" gym_steer;
        //bind Mouse1  ""gym_steer_kill"" gym_steer;

        bind F1 ""cl_toggle_help"";
        bind F2 ""cl_set_state play"";
        bind F3 ""cl_set_state edit ; ed_set_state pather_test"";
        bind F4 ""cl_set_state edit ; ed_set_state place_towers"";
        bind F5 ""cl_set_state edit ; ed_set_state place_turrets"";
        bind F6 ""cl_set_state edit ; ed_set_state atk_pos_solver"";
        bind F7 ""cl_set_state edit ; ed_set_state place_terrain"";
        bind F8 ""cl_set_state edit ; ed_set_state place_spawn_zones"";
        bind F9 ""cl_set_state gym ; gym_set_state steer"";

        //bind A +client_pan_left;
        //bind D +client_pan_right;
        //bind S +client_pan_down;
        //bind W +client_pan_up;

        sdl_screen_height 1042;
        sdl_screen_width 902;
        sdl_screen_x 513
        sdl_screen_y 36
    " );

    Qonsole.onStoreCfg_f = () => KeyBinds.StoreConfig();

    KeyBinds.Log = s => Qonsole.Log( "Keybinds: " + s );
    KeyBinds.Error = s => Qonsole.Error( "Keybinds: " + s );
    QGL.Log = o => Qonsole.Log( "QGL: " + o );
    QGL.Error = s => Qonsole.Error( "QGL: " + s );

}

static void QonsolePostStart_kmd( string [] argv ) {
    _initialized = false;

    try {

    if ( ! Application.isPlaying ) {
        return;
    }

    if ( Cl.IsLocalGame() || LocalServerAlwaysOn_kvar ) {
        if ( ! Sv.Init( svh: "[FFA000]Server: [-]" ) ) {
            return;
        }
    }

    if ( ! Cl.Init() ) {
        return;
    }

    _clockDate = DateTime.UtcNow;
    _clockPrevDate = DateTime.UtcNow;
    _initialized = true;

    } catch ( Exception e ) {
        Qonsole.Error( e );
    }
}

static DateTime _clockDate, _clockPrevDate;
static void QonsoleTick_kmd( string [] argv ) {
    if ( ! _initialized ) {
        return;
    }

    try {

    _clockDate = DateTime.UtcNow;
    // using integers leads to client outrunning the server clock
    double timeDelta = ( _clockDate - _clockPrevDate ).TotalMilliseconds;
    int timeDeltaMs = ( int )timeDelta;
    _clockPrevDate = _clockDate;

    if ( Cl.IsLocalGame() ) {
        Sv.RunLocalServer( timeDeltaMs );
    }

    Cl.Tick( timeDelta );

    if ( ShowFrameTime_kvar ) {
        QGL.LatePrint( ( ( int )( Time.deltaTime * 1000 ) ).ToString("000"),
                                                                            Screen.width - 50, 20 );
    }

#if false
    QGL.LateBlit( AppleFont.GetTexture(), 100, 100, 100, 100, angle: Time.time * 5f );
    var pts = new Vector2 [] { 
        new Vector2( 100, 100 ),
        new Vector2( 200, 100 ),
        new Vector2( 300, 300 ),
        new Vector2( 100, 200 ),
    };
    QGL.LateDrawLineLoop( pts, color: Color.yellow );
#endif

    } catch ( Exception e ) {
        Qonsole.Error( e );
    }
}

static void QonsoleDone_kmd( string [] argv ) {
    Cl.Done();
    Sv.Done();
}


}
