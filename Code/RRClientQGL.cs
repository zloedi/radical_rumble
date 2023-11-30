using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;

static class RRClientQGL {


public static Vector2 mousePosition = new Vector2( -1, -1 );
public static Vector2 mouseDelta;

public static bool mouse0Held;
public static bool mouse0Up;
public static bool mouse0Down;

public static bool mouse1Held;
public static bool mouse1Up;
public static bool mouse1Down;

public static bool isPaused = false;

static HashSet<KeyCode> _holdKeys = new HashSet<KeyCode>();

public static void Log( object o ) {
    ZClient.Log( o.ToString() );
}

public static void Log( string s ) {
    ZClient.Log( s );
}

public static void Error( string s ) {
    ZClient.Error( s );
}

public static bool Init() {
    //QUI.DrawLineRect = (x,y,w,h) => QGL.LateDrawLineRect(x,y,w,h,color:Color.magenta);
    QUI.Log = s => ZClient.Log( s );
    QUI.Error = s => ZClient.Error( s );
    return true;
}

public static void Done() {
}

public static void Tick( int timeDeltaMs ) {
    WrapBox.DisableCanvasScale();

    mouse0Up = mouse0Down = false;
    mouse1Up = mouse1Down = false;
    mouseDelta = Vector2.zero;

    if ( Qonsole.Active || ! Application.isFocused ) {
        _holdKeys.Clear();
    } else {
        Vector2 mp = new Vector2( Input.mousePosition.x, Screen.height - Input.mousePosition.y );

        // don't spill out of the window so we can test with multiple clients on the same screen
        if ( mp.x >= 0 && mp.x < Screen.width && mp.y >= 0 && mp.y < Screen.height ) {
            mouseDelta = mp - mousePosition;
            mousePosition = mp;
        }

        foreach ( var kc in KeyBinds.keys ) {
            if ( Input.GetKeyDown( kc ) ) {

                if ( kc == KeyCode.Mouse0 ) {
                    QUI.OnMouseButton( true );
                    //Draw.bottomError = null;
                }

                if ( ! isPaused && QUI.hotWidget == 0 && QUI.activeWidget == 0 ) {
                    mouse0Down = kc == KeyCode.Mouse0;
                    mouse1Down = kc == KeyCode.Mouse1;
                    if ( mouse0Down ) mouse0Held = true;
                    if ( mouse1Down ) mouse1Held = true;
                } else {
                    mouse0Held = false;
                    mouse1Held = false;
                }
                KeyBinds.TryExecuteBinds( keyDown: kc );
                _holdKeys.Add( kc );
            }

            if ( Input.GetKeyUp( kc ) ) {
                if ( kc == KeyCode.Mouse0 ) QUI.OnMouseButton( false );

                mouse0Up = kc == KeyCode.Mouse0;
                mouse1Up = kc == KeyCode.Mouse1;
                if ( mouse0Up ) mouse0Held = false;
                if ( mouse1Up ) mouse1Held = false;
                KeyBinds.TryExecuteBinds( keyUp: kc );
                _holdKeys.Remove( kc );
            }
        } 

        foreach ( var k in _holdKeys ) {
            KeyBinds.TryExecuteBinds( keyHold: k );
        }
    }

    QUI.Begin( ( int )mousePosition.x, ( int )mousePosition.y );

    Draw.wboxScreen = new WrapBox{ w = Screen.width, h = Screen.height };

    Draw.boardW = game.board.width;
    Draw.solid = game.board.filter.solid;
    Draw.no_solid = game.board.filter.no_solid;
    Draw.centralBigRedMessage = null;

    Draw.FillScreen();

    DrawBoard();

    //if ( ! mouse0Down && ! mouse1Down && AllowSpam() ) {
    //    // ! make sure we update the same set (i.e. selected) on the server too !
    //    // ! otherwise this will produce a lot of traffic !
    //    foreach ( var z in filter.zSelectedLocal ) {
    //        if ( pawn.Cursor( z ) != mouseHexCoord ) {
    //            UpdateCursorRemote();
    //            break;
    //        }
    //    }
    //}

    QUI.End();

    //if ( mouse0Down || mouse1Down ) {
    //    UpdateFiltersLocal();
    //}

    //if ( ! isPaused && QUI.activeWidget != 0 ) {
    //    UpdateFiltersLocal();
    //}

    if ( Cellophane.VarChanged( nameof( ClState_kvar ) ) ) {
        Color c = Color.white;
        c.a = 3;
        string state = _tickNames[ClState_kvar % _ticks.Length];
        SingleShot.AddConditional( dt => {
            string txt = $"State: {state}";
            Draw.OutlinedTextCenter( Screen.width / 2, Screen.height / 2, txt, color: c, scale: 2 );
            c.a -= dt;
            return c.a > 0;
        } );
        Log( $"Changed state to {state}" );
    }

    if ( ZClient.state != ZClient.State.Connected ) {
        Draw.FillScreen( new Color( 0, 0, 0, 0.75f ) );
        Draw.centralBigRedMessage = "Connecting to server...";
        Draw.BigRedMessage();
    }
    
    SingleShot.TickMs( timeDeltaMs );
}

public static void DrawBoard( Color? colorSolid = null ) {
    Draw.Board( colorSolid );
}


}
