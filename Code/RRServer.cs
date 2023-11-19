using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

static class RRServer {


public const int PULSE_TIME = 3333;
public const int TICK_TIME = 100;

[Description( "0 -- minimal network logging, 1 -- some network logging, 2 -- detailed network logging, 3 -- full network logging " )]
static int SvTraceLevel_kvar = 1;

[Description("Print outgoing packets on game Tick(): 1 -- persistent and non-delta only; 2 -- all")]
static bool SvPrintOutgoingPackets_kvar = false;

[Description("Send log messages to clients.")]
static bool SvBounceLog_kvar = false;
[Description("Send log errors to clients.")]
static bool SvBounceError_kvar = false;

public static Game game = new Game();

static List<byte> _sentPacket = new List<byte>();
// commands appended to the game delta and send to the clients with the next Tick()
static List<string> _trailCommands = new List<string>();

static string _logHeader = string.Empty;
static bool _logTimestamp = false;

public static void Log( object o ) {
    Log( o.ToString() );
}

public static void Log( string s ) {
    if ( SvTraceLevel_kvar == 0 ) {
        return;
    }
    string time = "";
    if ( _logTimestamp ) {
        time = DateTime.Now.ToString("dd/MM HH:mm:ss");
    }
    Qonsole.Log( $"{_logHeader}{time}: {s}" );
    if ( SvBounceLog_kvar ) {
        string stripped = Cellophane.ColorTagStripAll( s );
        _trailCommands.Add( $"echo \"[00ffff]ServerLog: {stripped}[-]\"" );
    }
}

public static void Error( string s ) {
    if ( SvTraceLevel_kvar == 0 ) {
        return;
    }
    // header allows color tags, ignore them on the server
    string svhErr = Cellophane.ColorTagStripAll( _logHeader );
    Qonsole.Error( $"{svhErr}{s}" );
    if ( SvBounceError_kvar ) {
        _trailCommands.Add( $"echo \"[ff0000]ServerError: {s}[-]\"" );
    }
}

public static void RegisterTrail( string cmd ) {
    _trailCommands.Add( cmd );
}

public static bool Init( string svh = "Server: ", bool logTimestamps = false ) {
    _logHeader = svh;
    _logTimestamp = logTimestamps;

    if ( SvTraceLevel_kvar >= 0 ) {
        game.Log = RRServer.Log;
        game.Error = RRServer.Error;
        ZServer.Error = game.Error;
        ZServer.net.Error = game.Error;
    }

    if ( SvTraceLevel_kvar >= 1 ) {
        ZServer.Log = game.Log;
    }

    if ( SvTraceLevel_kvar >= 2 ) {
        ZServer.net.Log = game.Log;
    }

    if ( SvTraceLevel_kvar >= 3 ) {
        ZServer.LogChan = game.Log;
    }

    if ( ! game.Init() ) {
        return false;
    }

    ZServer.net.TryExecuteOOB = s => Qonsole.TryExecute( s );
    ZServer.onClientCommand_f = (zport,cmd) => RRServer.Execute( zport, cmd );
    ZServer.onTick_f = RRServer.Tick;
    ZServer.onClientDisconnect_f = zport => {};
    ZServer.onClientConnect_f = zport => {};

    return ZServer.Init();
}

// returns game state delta followed by any explicit commands to clients
public static List<byte> Tick( int dt, bool needPacket ) {
    _sentPacket.Clear();

    string packet = DeltaGameState();
    foreach ( var tc in _trailCommands ) {
        packet += $"; {tc}";
    }
    _trailCommands.Clear();

    if ( string.IsNullOrEmpty( packet ) ) {
        return _sentPacket;
    }

    if ( SvPrintOutgoingPackets_kvar ) {
        Log( $"outgoing packet: {packet} len: {packet.Length}" );
    }

    _sentPacket.AddRange( Encoding.UTF8.GetBytes( packet ) );

    return _sentPacket;
}

public static void Execute( int zport, string command ) {
    try {
        Cellophane.TryExecuteString( command, context: zport );
    } catch ( Exception e ) {
        Error( e.ToString() );
    }
}

static string DeltaGameState() {
    string delta = "";
    foreach ( var row in game.syncedRows ) {
        Shadow.Row shadowRow = game.shadow.arrayToShadow[row];
        if ( shadowRow.type == Shadow.DeltaType.Uint8 ) {
            if ( Delta.DeltaBytes( ( byte[] )row, ( byte[] )shadowRow.array, out string changes,
                                                out string values, maxInput: shadowRow.maxRow ) ) {
                delta += shadowRow.name + changes + " :" + values + " : ";
            }
        } else if ( shadowRow.type == Shadow.DeltaType.Uint16 ) {
            if ( Delta.DeltaShorts( ( ushort[] )row, ( ushort[] )shadowRow.array,
                            out string changes, out string values, maxInput: shadowRow.maxRow ) ) {
                delta += shadowRow.name + changes + " :" + values + " : ";
            }
        } else if ( shadowRow.type == Shadow.DeltaType.Int32 ) {
            if ( Delta.DeltaInts( ( int[] )row, ( int[] )shadowRow.array,
                            out string changes, out string values, maxInput: shadowRow.maxRow ) ) {
                delta += shadowRow.name + changes + " :" + values + " : ";
            }
        }
    }
    return delta;
}

// == commands ==


static void SvBroadcastCommand_kmd( string [] argv, int zport ) {
    string cmd = "";
    for ( int i = 1; i < argv.Length; i++ ) {
        cmd += $"\"{argv[i]}\";";
    }
    _trailCommands.Add( cmd );
}

static void SvExecute_kmd( string [] argv, int zport ) {
    if ( argv.Length < 2 ) {
        Log( "{argv[0]}: No command supplied." );
        return;
    }
    Log( $"Executing a command from {zport}" );
    string [] newArgv = new string[argv.Length - 1];
    Array.Copy( argv, 1, newArgv, 0, newArgv.Length );
    if ( Array.IndexOf( newArgv, argv[0] ) >= 0 ) {
        Error( "{argv[0]}: Can't be recursive." );
        return;
    }
    Cellophane.TryExecute( newArgv, zport );
}

static void SvPrintClients_kmd( string [] argv ) {
    foreach ( var c in ZServer.clients ) {
        Log( $"endpoint: {c.endPoint}; zport: {c.netChan.zport}" ); 
    }
}


}
