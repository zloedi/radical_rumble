using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

static class RRClient {


[Description( @"Connect to this server for multiplayer.
// 127.0.0.1 -- this is a local game and the host is this machine
// 89.190.193.149 -- raspberry pi server."
)]
public static string ClServerIpAddress_kvar = "89.190.193.149";

[Description( "0 -- minimal network logging, 1 -- some network logging, 2 -- detailed network logging, 3 -- full network logging " )]
public static int ClTraceLevel_kvar = 1;

[Description("Print incoming packets: 1 -- some; 2 -- all")]
static int ClPrintIncomingPackets_kvar = 0;

public static Game game = new Game();

public static Vector2 mousePosition = new Vector2( -1, -1 );
public static Vector2 mouseDelta;

public static bool mouse0Held;
public static bool mouse0Up;
public static bool mouse0Down;

public static bool mouse1Held;
public static bool mouse1Up;
public static bool mouse1Down;

public static bool isPaused = false;

public static Board board => game.board;

static HashSet<KeyCode> _holdKeys = new HashSet<KeyCode>();
static Action _tick = () => {};

[Description( "0 -- play; 1 -- map editor" )]
static int ClState_kvar = 0;
static bool ClPrintOutgoingCommands_kvar = false;
static string [] _tickNames = { "Play",           "Map Editor", };
static Action [] _ticks =     { () => DrawBoard(), MapEditor.Tick };

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
    // initialize the network
    UpdateTraceLevel();
    ZClient.net.TryExecuteOOB = s => Qonsole.TryExecute( s );
    if ( ! ZClient.Init( svIP: ClServerIpAddress_kvar ) ) {
        return false;
    }
    // could be invoked (mulitple times) on ZClient.Tick
    ZClient.onServerPacket_f = OnServerPacket;
    ZClient.onConnected_f = OnConnected;

    //QUI.DrawLineRect = (x,y,w,h) => QGL.LateDrawLineRect(x,y,w,h,color:Color.magenta);
    QUI.Log = s => ZClient.Log( s );
    QUI.Error = s => ZClient.Error( s );

    // we don't need shadow copies of game state on the client
    return game.Init( skipShadowClones: true );
}

public static void Done() {
    ZClient.Done();
}

public static void Tick( int timeDeltaMs ) {
    ZClient.Tick( timeDeltaMs );

    WrapBox.DisableCanvasScale();

    if ( Cellophane.VarChanged( nameof( ClServerIpAddress_kvar ) ) ) {
        ZClient.Reset( ClServerIpAddress_kvar );
        game.Reset();
    }

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
    //Draw.UpdateBoardBounds();
    //Draw.UpdatePanning();

    Draw.centralBigRedMessage = null;

    Draw.FillScreen();

    _ticks[ClState_kvar % _ticks.Length]();

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

public static void Execute( string command ) {
    try {
        Cellophane.TryExecuteString( command );
    } catch ( Exception e ) {
        Error( e.ToString() );
    }
}

public static bool IsLocalGame() {
    if ( ClServerIpAddress_kvar.Contains( "127.0.0.1" ) ) {
        return true;
    }
    if ( ZServer.net.GetLocalIPAddress( out string ip ) ) {
        return ClServerIpAddress_kvar.Contains( ip );
    }
    return false;
}

static void UpdateTraceLevel() {
    ZClient.Log = s => {};
    ZClient.Error = s => {};
    ZClient.net.Log = s => {};
    ZClient.net.Error = s => {};
    ZClient.netChan.Log = s => {};

    if ( ClTraceLevel_kvar >= 0 ) {
        game.Log = s => Qonsole.Log( $"[00C0FF]Client:[-] {s}" );
        game.Error = s => Qonsole.Error( $"Client: {s}" );
        ZClient.Error = game.Error;
        ZClient.net.Error = game.Error;
    }

    if ( ClTraceLevel_kvar >= 1 ) {
        ZClient.Log = game.Log;
    }

    if ( ClTraceLevel_kvar >= 2 ) {
        ZClient.net.Log = game.Log;
    }

    if ( ClTraceLevel_kvar >= 3 ) {
        ZClient.netChan.Log = game.Log;
    }
}

static void OnConnected() {
    game.Reset();
}

static void OnServerPacket( List<byte> packet ) {
    if ( packet.Count == 0 ) {
        if ( ClPrintIncomingPackets_kvar == 2 ) {
            Qonsole.Print( "." );
        }
        return;
    }

    // FIXME: try to cutout the delta from the packet without building a string out of it
    // FIXME: or even better -- move to raw compressed bytes for the delta

    string packetStr = System.Text.Encoding.UTF8.GetString( packet.ToArray() );

    // cut out the first command and execute it; should be delta until the first semicolon
    string deltaCmd = "";
    int deltaNumChars = 0;
    for ( int i = 0; i < packetStr.Length; i++ ) {
        deltaNumChars++;
        if ( packetStr[i] == ';' ) {
            break;
        }
        deltaCmd += packetStr[i];
    }

    string trailingCommands = packetStr.Substring( deltaNumChars ).TrimStart();

    if ( ! Cellophane.GetArgvBare( deltaCmd, out string [] argv )
                                                                && trailingCommands.Length == 0 ) {
        Error( $"Packet: {packetStr}" );
        Error( $"Delta Cmd: {deltaCmd}" );
        Error( "Expected game delta and/or trail commands. Dropping the packet." );
        return;
    }

    // apply server game state on the client
    if ( game.UndeltaState( argv, out bool updateBoardFilters ) ) {
        if ( updateBoardFilters ) {
            game.board.UpdateFilters();
        } 
    }

    if ( ClPrintIncomingPackets_kvar == 1 ) {
        Log( $"incoming packet: '{packetStr}'" );
    }

    // maybe there are some trailing commands after the delta, try to execute them here

    // really messes up when there is 'echo' inside 'echo'
    // i.e. when server is set to bounce logs and the bounce is logged with the packet
    Cellophane.SplitCommands( trailingCommands, out string [] cmds );
    foreach ( var c in cmds ) {
        Execute( c );
    }
}

// some actions can potentially flood the net channel
// use this to block them until proper time
public static bool AllowSpam() {
    return ! ZClient.HasUnsentReliableCommands();
}

public static void DrawBoard( Color? colorSolid = null ) {
    Draw.Board( board.width, board.filter.solid, board.filter.no_solid, colorSolid );
}

public static void SvCmd( string cmd ) {
    if ( ClPrintOutgoingCommands_kvar ) {
        Log( cmd );
    }
    ZClient.RegisterReliableCmd( cmd );
}

// == commands ==

static DateTime _pingStart;

static void Ping_kmd( string [] argv ) {
    Log( $"pinging {ClServerIpAddress_kvar}" );
    _pingStart = DateTime.UtcNow;
    SvCmd( "sv_ping" );
}

static void Pong_kmd( string [] argv ) {
    if ( argv.Length < 2 ) {
        Error( $"{argv[0]} Needs zport." );
        return;
    }
    int.TryParse( argv[1], out int zport );
    if ( zport != ZClient.netChan.zport ) {
        return;
    }
    double ping = ( DateTime.UtcNow - _pingStart ).TotalMilliseconds;
    Log( $"ping: {ping} milliseconds" );
}


}
