using System;

using UnityEngine;

using Cl = RRClient;
using Sv = RRServer;

static class Main {

static bool LocalServerAlwaysOn_kvar = false;

static bool _initialized = false;

static void QonsolePreConfig_kmd( string [] argv ) {
    Cellophane.ConfigVersion_kvar = 1;
    Qonsole.TryExecute( @"
        bind Alpha1 ""cl_spawn Brute"" play;
        bind Alpha2 ""cl_spawn Archer"" play;
        bind Alpha3 ""cl_spawn Flyer"" play;
        bind Alpha4 ""cl_spawn Brute 1"" play;
        bind Alpha5 ""cl_spawn Archer 1"" play;
        bind Alpha6 ""cl_spawn Flyer 1"" play;
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

        bind F1 ""cl_set_state play"";
        bind F2 ""cl_set_state edit ; ed_set_state pather_test"";
        bind F3 ""cl_set_state edit ; ed_set_state place_towers"";
        bind F4 ""cl_set_state edit ; ed_set_state place_turrets"";
        bind F5 ""cl_set_state edit ; ed_set_state atk_pos_solver"";
        bind F6 ""cl_set_state edit ; ed_set_state place_terrain"";

        bind F7 ""cl_set_state gym ; gym_set_state steer"";

        //bind A +client_pan_left;
        //bind D +client_pan_right;
        //bind S +client_pan_down;
        //bind W +client_pan_up;
    " );
    Qonsole.onStoreCfg_f = () => KeyBinds.StoreConfig();
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

    } catch ( Exception e ) {
        Qonsole.Error( e );
    }
}

static void QonsoleDone_kmd( string [] argv ) {
    Cl.Done();
    Sv.Done();
}


}
