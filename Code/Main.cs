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
        bind Alpha1 ""cl_spawn Brute"";
        bind Alpha2 ""cl_spawn Archer"";
        bind Alpha3 ""cl_spawn Flyer"";

        bind Alpha4 ""cl_spawn Brute 1"";
        bind Alpha5 ""cl_spawn Archer 1"";
        bind Alpha6 ""cl_spawn Flyer 1"";

        bind F1 ""cl_set_state play"";
        bind F2 ""cl_set_state edit ; map_editor_set_state place_towers"";
        bind F3 ""cl_set_state edit ; map_editor_set_state pather_test"";

        bind K cl_kill;

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
