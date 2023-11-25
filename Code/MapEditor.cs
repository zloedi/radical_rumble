using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

using Cl = RRClient;

static class MapEditor {


[Description( "0 -- none; 1 -- place terrain; 2 -- place start positions" )]
static int State_cvar = 0;
static string LastSavedMap_cvar = "unnamed";

static string [] _tickNames = { "None",  "Place Terrain",  "Place Tower" };
static Action [] _ticks =     { None_tck, PlaceTerrain_tck, PlaceTower_tck };

static string _stateName => _tickNames[State_cvar % _ticks.Length];
static Vector2Int _mouseHexCoord;
static bool _mouseHexChanged;

static bool CanClick => QUI.hotWidget == 0 && QUI.activeWidget == 0;

public static void Tick() {
    Vector2Int newHex = ScreenPosToHexCoord( Cl.mousePosition );
    _mouseHexChanged = ( _mouseHexCoord - newHex ).sqrMagnitude > 0;
    _mouseHexCoord = newHex;
    Cl.DrawBoard();
    _ticks[State_cvar % _ticks.Length]();
}

static void None_tck() {
}

static void PlaceTerrain_tck() {
    //Draw.HexesBoard();
    //Draw.PlayerCursors();
    //Draw.HexesStart();

    if ( ! CanClick ) {
        return;
    }

    if ( Cl.mouse0Down || ( Cl.mouse0Held && _mouseHexChanged && Cl.AllowSpam() ) ) {
        Vector2Int hxc = ScreenPosToHexCoord( Cl.mousePosition );
        Cl.SvCmd( $"sv_set_terrain {hxc.x} {hxc.y} 128" );
    }

    if ( Cl.mouse1Down || ( Cl.mouse1Held && _mouseHexChanged && Cl.AllowSpam() ) ) {
        Vector2Int hxc = ScreenPosToHexCoord( Cl.mousePosition );
        Cl.SvCmd( $"sv_set_terrain {hxc.x} {hxc.y} 0" );
    }
}

static void PlaceTower_tck() {
    //Draw.HexesBoard( voidsAlpha: 0.2f );
    //Draw.PlayerCursors();
    //Draw.HexesStart();

    //if ( ! CanClick ) {
    //    return;
    //}

    //int hx = Cl.mouseHex;

    //if ( ! Cl.game.board.IsInBounds( hx ) ) {
    //    return;
    //}

    //if ( Cl.mouse0Down ) {
    //    if ( Cl.game.board.IsSolid( hx ) ) {
    //        Cl.SvCmd( $"server_set_start_hex {hx} 1" );
    //        _errorMessage = null;
    //    } else {
    //        _errorMessage = "Place the cursor on a solid hex.";
    //    }
    //}

    //if ( Cl.mouse1Down ) {
    //    if ( Cl.game.board.HasStartFlag( hx ) ) {
    //        Cl.SvCmd( $"server_set_start_hex {hx} 0" );
    //        _errorMessage = null;
    //    } else {
    //        _errorMessage = "Place the cursor on a start hex.";
    //    }
    //}
}

static Vector2Int ScreenPosToHexCoord( Vector2 screenPos ) {
    return Hexes.ScreenToHex( screenPos, 12 * Draw.pixelSize );
}

static void Save_cmd( string [] argv ) {
    if ( argv.Length >= 2 ) {
        LastSavedMap_cvar = argv[1];
        if ( ! LastSavedMap_cvar.EndsWith( ".map" ) ) {
            LastSavedMap_cvar += ".map";
        }
    }
    Cl.SvCmd( $"sv_save_map {LastSavedMap_cvar}" );
}

static void Load_cmd( string [] argv ) {
    if ( argv.Length >= 2 ) {
        LastSavedMap_cvar = argv[1];
        if ( ! LastSavedMap_cvar.EndsWith( ".map" ) ) {
            LastSavedMap_cvar += ".map";
        }
    }
    Cl.SvCmd( $"sv_load_map {LastSavedMap_cvar}" );
}


}
