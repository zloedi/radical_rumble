using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
//using SDLPorts;
#endif

static class RRClient {


[Description( @"Connect to this server for multiplayer.
// 127.0.0.1 -- this is a local game and the host is this machine
// 89.190.193.149 -- raspberry pi server."
)]
public static string ClServerIpAddress_kvar = "127.0.0.1";

[Description( "0 -- minimal network logging, 1 -- some network logging, 2 -- detailed network logging, 3 -- full network logging " )]
public static int ClTraceLevel_kvar = 1;
[Description( "0 -- no clock cmd logging, 1 -- log only pathologic clocks, 2 -- log all clocks" )]
public static int ClLogClocks_kvar = 0;
[Description("Print incoming packets: 1 -- some; 2 -- all")]
static int ClPrintIncomingPackets_kvar = 0;
[Description("Sleep each frame in milliseconds.")]
public static int ClFrameSleep_kvar = 0;

static bool ShowBoardBounds_kvar = false;

public static Game game = new Game();

public static Vector2 mousePosScreen = new Vector2( -1, -1 );
public static Vector2 mousePosGame = new Vector2( -1, -1 );
public static Vector2Int mousePosAxial = new Vector2Int( -1, -1 );
public static int mouseHex;
public static bool mouseHexChanged;
public static Vector2 mouseDelta;

public static bool mouse0Held;
public static bool mouse0Up;
public static bool mouse0Down;

public static bool mouse1Held;
public static bool mouse1Up;
public static bool mouse1Down;

public static double clock, clockPrev, clockDeltaDbl;
public static int clockDelta;

// last received clock in Clk_kmd
public static int serverClock;

public static Board board => game.board;

static HashSet<KeyCode> _holdKeys = new HashSet<KeyCode>();

static int ClState_kvar = 0;
static bool ClPrintOutgoingCommands_kvar = false;
static string [] _tickNames;
static Action [] _ticks = TickUtil.RegisterTicksOfClass( typeof( RRClient ), out _tickNames );

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
    TickUtil.Log = QUI.Log = s => ZClient.Log( "TickUtil: " + s );
    TickUtil.Error = QUI.Error = s => ZClient.Error( "TickUtil: " + s );

    // we don't need shadow copies of game state on the client
    return game.Init( skipShadowClones: true );
}

public static void Done() {
    ZClient.Done();
}

static string _bindsText = "";
public static void TickKeybinds( string context = null ) {
    if ( Qonsole.Active || ! Application.isFocused ) {
        return;
    }

    foreach ( var kc in KeyBinds.keys ) {
        if ( ! Input.GetKeyDown( kc ) ) {
            continue;
        }
        KeyBinds.TryExecuteBinds( keyDown: kc, context: context );
        _holdKeys.Add( kc );
    }

    foreach ( var kc in KeyBinds.keys ) {
        if ( ! Input.GetKeyUp( kc ) ) {
            continue;
        }
        KeyBinds.TryExecuteBinds( keyUp: kc, context: context );
        _holdKeys.Remove( kc );
    } 

    foreach ( var k in _holdKeys ) {
        KeyBinds.TryExecuteBinds( keyHold: k, context: context );
    }

    _bindsText += $"\ncontext: '{context}'\n";
    foreach ( var kc in KeyBinds.keys ) {
        if ( KeyBinds.GetCmd( kc, context, out string cmd ) ) {
            _bindsText += $"{kc} -- {cmd}\n";
        }
    }
}

public static void Tick( double timeDeltaDbl ) {
    if ( ClFrameSleep_kvar > 0 ) {
        System.Threading.Thread.Sleep( Mathf.Min( 33, ClFrameSleep_kvar ) );
    }

    clockDeltaDbl = clock - clockPrev;
    clockPrev = clock;
    clock += timeDeltaDbl;

    clockDelta = ( int )clockDeltaDbl;

    WrapBox.DisableCanvasScale();

    Draw.wboxScreen = new WrapBox{ w = Screen.width, h = Screen.height };
    Draw.centralBigRedMessage = null;

    if ( Cellophane.VarChanged( nameof( ClServerIpAddress_kvar ) ) ) {
        ZClient.Reset( ClServerIpAddress_kvar );
        game.Reset();
    }

    InputBegin();

    _ticks[ClState_kvar % _ticks.Length]();

    // might change the clock
    ZClient.Tick( clockDelta );

    WBUI.QGLTextOutlined( _bindsText, Draw.wboxScreen, align: 2, color: Color.white, fontSize: 1 );
    if ( ShowBoardBounds_kvar ) {
        Draw.BoardBounds();
    }

#if false
    QUI.End();
#else
    InputEnd();
#endif

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
    
    SingleShot.TickMs( clockDelta );
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

// some actions can potentially flood the net channel
// use this to block them until proper time
public static bool AllowSpam() {
    return ! ZClient.HasUnsentReliableCommands();
}

public static void DrawBoard( Color? colorSolid = null ) {
    Draw.Board( colorSolid );
}

public static void SvCmd( string cmd ) {
    if ( ClPrintOutgoingCommands_kvar ) {
        Log( cmd );
    }
    ZClient.RegisterReliableCmd( cmd );
}

static void InputBegin() {
    _bindsText = "";

    mouse0Up = mouse0Down = false;
    mouse1Up = mouse1Down = false;
    mouseDelta = Vector2.zero;

    if ( Qonsole.Active || ! Application.isFocused ) {
        _holdKeys.Clear();
        return;
    }

    Vector2 mp = new Vector2( Input.mousePosition.x, Screen.height - Input.mousePosition.y );

    // don't spill out of the window so we can test with multiple clients on the same screen
    if ( mp.x >= 0 && mp.x < Screen.width && mp.y >= 0 && mp.y < Screen.height ) {
        mouseDelta = mp - mousePosScreen;
        mousePosScreen = mp;
        mousePosGame = Draw.ScreenToGamePosition( mp );
        mousePosAxial = Draw.ScreenToAxial( mp );
        int hx = board.Hex( mousePosAxial );
        mouseHexChanged = hx - mouseHex != 0;
        mouseHex = hx;
    }

    if ( ! mouse0Held && Input.GetKeyDown( KeyCode.Mouse0 ) ) {
        QUI.OnMouseButton( true );
    }

    if ( ! mouse0Held && Input.GetKeyUp( KeyCode.Mouse0 ) ) {
        QUI.OnMouseButton( false );
    }

    if ( QUI.hotWidget == 0 && QUI.activeWidget == 0 ) {
        if ( Input.GetKeyDown( KeyCode.Mouse0 ) ) {
            mouse0Held = true;
            mouse0Down = true;
        }
        if ( Input.GetKeyDown( KeyCode.Mouse1 ) ) {
            mouse1Held = true;
            mouse1Down = true;
        }
        if ( Input.GetKeyUp( KeyCode.Mouse0 ) ) {
            mouse0Held = false;
            mouse0Up = true;
        }
        if ( Input.GetKeyUp( KeyCode.Mouse1 ) ) {
            mouse1Held = false;
            mouse1Up = true;
        }
    }

    if ( QUI.activeWidget != 0 ) {
        mouse0Held = false;
        mouse1Held = false;
    }

    // tick the global (no context) keybinds
    TickKeybinds();

    QUI.Begin( ( int )mousePosScreen.x, ( int )mousePosScreen.y );
}

static void InputEnd() {
    QUI.End();
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
    game.UndeltaState( argv, ( int )clock, out bool updateBoard );

    if ( ClPrintIncomingPackets_kvar > 1 ) {
        Log( $"incoming packet: '{packetStr}'" );
    } else if ( ClPrintIncomingPackets_kvar == 1 ) {
        if ( deltaCmd.Length > 0 ) {
            Log( $"incoming packet: '{packetStr}'" );
        }
    }

    // maybe there are some trailing commands after the delta, try to execute them here

    // really messes up when there is 'echo' inside 'echo'
    // i.e. when server is set to bounce logs and the bounce is logged with the packet
    Cellophane.SplitCommands( trailingCommands, out string [] cmds );
    foreach ( var c in cmds ) {
        Execute( c );
    }
}

static void Play_tck() {
    PlayerQGL.Tick();
}

static void Edit_tck() {
    MapEditor.Tick();
}

static void Gym_tck() {
    Gym.Tick();
}

// == commands ==

static void Clk_kmd( string [] argv ) {
    if ( argv.Length < 2 ) {
        Error( "supply a clock value" );
        return;
    }

    int clClk = ( int )clock;
    int.TryParse( argv[1], out serverClock );
    int delta = serverClock - clClk;

    const int pathologic = 250;

    if ( ClLogClocks_kvar >= 2 ) {
        Log( $"{argv[0]}: cl clock: {clClk}" );
        Log( $"{argv[0]}: sv clock: {serverClock}" );
        Log( $"{argv[0]}: delta: {delta}" );
    } else if ( ClLogClocks_kvar >= 1 ) {
        if ( Mathf.Abs( delta ) > pathologic ) {
            Log( $"[ffc000]{argv[0]}: cl clock: {clClk}[-]" );
            Log( $"[ffc000]{argv[0]}: sv clock: {serverClock}[-]" );
            Log( $"[ffc000]{argv[0]}: delta: {delta}[-]" );
        }
    }

    if ( delta > 0 ) {
        if ( delta > pathologic ) {
            // the server clock is too far ahead, snap client to this time
            clockPrev = clock = serverClock;
        } else {
            // this will increase the delta next tick
            clock = serverClock;
        }
    } else if ( delta < -pathologic ) {
        // the server clock is too far behind, snap client to this time
        clockPrev = clock = serverClock;
    }
}

static void PrintHex_kmd( string [] argv ) { 
    Qonsole.Log( $"hx: {mouseHex} axial: {mousePosAxial} game: {mousePosGame}" );
}

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

static void ClBoardMoved_kmd( string [] argv ) {
    if ( argv.Length < 3 ) {
        Qonsole.Error( $"{argv[0]} needs x y" );
        return;
    }

    int.TryParse( argv[1], out int x );
    int.TryParse( argv[2], out int y );

    Vector2 scr = Draw.AxialToScreenNoPan( x, y );
    Draw.OffsetView( scr );
}

static void ClCenterBoard_kmd( string [] argv ) {
    Draw.CenterBoardOnScreen();
}

static void ClSpawn_kmd( string [] argv ) {
    if ( argv.Length < 2 ) {
        Error( $"{argv[0]} <def_name> [team]" );
        return;
    }

    if ( ! Pawn.FindDefByName( argv[1], out Pawn.Def def ) ) {
        Log( $"{argv[0]} Can't find def named {argv[1]}" );
        return;
    }
    int team = 0;
    if ( argv.Length > 2 ) {
        int.TryParse( argv[2], out team );
    }
    SvCmd( $"sv_spawn {argv[1]} {Cellophane.FtoA( mousePosGame.x )} {Cellophane.FtoA( mousePosGame.y )} {team}" );
}

static void ClKill_kmd( string [] argv ) {
    foreach ( var z in game.pawn.filter.no_garbage ) {
        if ( ( game.pawn.mvPos[z] - mousePosGame ).sqrMagnitude <= 1 ) {
            SvCmd( $"sv_kill {z}" );
            break;
        }
    }
}

static void ClPrintPawns_kmd( string [] argv ) {
    for ( int z = 0; z < Pawn.MAX_PAWN; z++ ) {
        if ( game.pawn.IsGarbage( z ) ) {
            continue;
        }
        FieldInfo [] fields = typeof( Pawn ).GetFields();
        foreach ( FieldInfo fi in fields ) {
            Array a = fi.GetValue( game.pawn ) as Array;
            if ( a != null && a.Length == Pawn.MAX_PAWN ) {
                string val = a.GetValue( z ).ToString();
                if ( fi.Name == "def" ) {
                    val += $" ({Pawn.defs[game.pawn.def[z]].name})";
                }
                Qonsole.Log( $"{fi.Name}: {val}" );
            }
        }
        Qonsole.Log( "\n" );
    }
}

static void ClSetState_kmd( string [] argv ) { 
    TickUtil.SetState( argv, _ticks, _tickNames, ref ClState_kvar );
}


}
