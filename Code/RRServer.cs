using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

static class RRServer {

enum State {
    None,
    Wait,
    Play,
}

public const int PULSE_TIME = 3333;
public const int TICK_TIME = 100;

[Description( "0 -- minimal network logging, 1 -- some network logging, 2 -- detailed network logging, 3 -- full network logging " )]
static int SvTraceLevel_kvar = 1;

[Description("Print outgoing packets on game Tick(): 1 -- persistent and non-delta only; 2 -- all")]
static bool SvPrintOutgoingPackets_kvar = false;
static bool SvPrintIncomingCommands_kvar = false;
[Description("Send log messages to clients.")]
static bool SvBounceLog_kvar = false;
[Description("Send log errors to clients.")]
static bool SvBounceError_kvar = false;
static string SvLastLoadedMap_kvar = "default.map";

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

    LoadLastMap();

    ZServer.net.TryExecuteOOB = s => Qonsole.TryExecute( s );
    ZServer.onClientCommand_f = (zport,cmd) => RRServer.Execute( zport, cmd );
    ZServer.onTick_f = RRServer.Tick;
    ZServer.onClientDisconnect_f = zport => {
        game.player.DestroyByZport( zport );
    };

    // FIXME: move to Game_sv
    // FIXME: resend the entire universe to everyone?
    ZServer.onClientConnect_f = zport => {
        game.shadow.ClearShadowRows();

        int pl = game.player.GetByZPort( zport );
        if ( pl != 0 ) {
            Log( $"Reconnecting player {pl}" );
            return;
        }

        if ( ! game.player.AnyTeamNeedsPlayers() ) {
            Log( "Game is full, client is an observer." );
            return;
        }

        pl = game.player.Create( zport, ZServer.clock );

        if ( pl != 0 ) {
            Log( $"Created player {pl} of team {game.player.team[pl]}." );
            // when a player joins reset the mana to all players
            for ( int plMana = 1; plMana < Player.MAX_PLAYER; plMana++ ) {
                game.player.ResetMana( plMana, ZServer.clock );
            }
        } else {
            Error( $"Failed to create player for client {zport}" );
        }
    };

    return ZServer.Init();
}

public static void Done() {
    ZServer.Done();
}

static int _pulse;
static int _localServerSleep;
// as opposed to standalone server process
public static void RunLocalServer( int timeDeltaMs ) {
    bool sendPacket = false;

    // will invoke any incoming commands on the server 'onClientCommand_f'
    while ( ZServer.Poll( out bool hadCommands ) ) {
        if ( hadCommands ) {
            // generate delta when any client command got executed on the server
            // and send the delta immediately
            sendPacket = true;
            break;
        }
    }

    if ( _pulse <= 0 ) {
        sendPacket = true;
    }

    if ( sendPacket || _localServerSleep <= 0 ) {
        // will invoke RRServer.Tick onTick_f and push any new packets
        ZServer.TickWithClocks( sendPacket );
        _localServerSleep = TICK_TIME;
    }
    
    _localServerSleep -= timeDeltaMs;
    _pulse = sendPacket ? PULSE_TIME : _pulse - timeDeltaMs;
}

// returns game state delta followed by any explicit commands to clients
public static List<byte> Tick( int dt, bool isForcedSend ) {

    // ==

    game.TickServer();

    // ==

    _sentPacket.Clear();

    string packet = DeltaGameState();

    foreach ( var tc in _trailCommands ) {
        packet += $"; {tc}";
    }
    _trailCommands.Clear();

    if ( string.IsNullOrEmpty( packet ) && ! isForcedSend ) {
        // no delta nor trailing commands
        return _sentPacket;
    }

    // if we do have a packet, append the clock
    packet += $"; clk {ZServer.clock};";

    if ( SvPrintOutgoingPackets_kvar ) {
        Log( $"outgoing packet: {packet} len: {packet.Length}" );
    }

    _sentPacket.AddRange( Encoding.UTF8.GetBytes( packet ) );

    return _sentPacket;
}

public static bool Execute( int zport, string command ) {
    try {
        if ( SvPrintIncomingCommands_kvar ) {
            Log( $"{zport}: {command}" );
        }
        return Cellophane.TryExecuteString( command, context: zport );
    } catch ( Exception e ) {
        Error( e.ToString() );
    }
    return false;
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

static string MapsDir() {
#if UNITY_STANDALONE
    string path;
    if ( Application.isEditor ) {
        path = Application.dataPath + "/../../BuildUnity/";
    } else {
        path = Application.dataPath + "/../";
    }
    return path;
#else
    string exeLocation = System.Reflection.Assembly.GetEntryAssembly().Location;
    return System.IO.Path.GetDirectoryName( exeLocation );
#endif
}

static void LoadLastMap() {
    if ( string.IsNullOrEmpty( SvLastLoadedMap_kvar ) ) {
        return;
    }
    string path = Path.Combine( MapsDir(), SvLastLoadedMap_kvar );
    string cmd = "";
    try {
        // sv_undelta will do game reset
        cmd = File.ReadAllText( path );
        game.Reset();
        Cellophane.TryExecuteString( cmd );
        game.PostLoadMap();
        Log( $"Loaded '{path}'" );
    } catch ( Exception ) {
        Error( $"Failed to read file '{path}'" );
    }
}

// == commands ==

static void SvPing_kmd( string [] argv, int zport ) {
    RegisterTrail( $"pong {zport}" );
}

static void SvLoadMap_kmd( string [] argv, int zport ) {
    if ( argv.Length < 2 ) {
        Error( $"{argv[0]} No filename supplied." );
        return;
    }
    SvLastLoadedMap_kvar = argv[1];
    LoadLastMap();
}

static void SvSaveMap_kmd( string [] argv, int zport ) {
    if ( argv.Length < 2 ) {
        Error( $"{argv[0]} No filename supplied." );
        return;
    }
    SvLastLoadedMap_kvar = argv[1];
    string path = Path.Combine( MapsDir(), SvLastLoadedMap_kvar );
    string storage = "sv_undelta\n";
    string emitRow( string name, string c, string v ) {
        return $"{name}{c} :{v} :\n";
    }
    foreach ( Array row in game.persistentRows ) {
        Shadow.Row shadowRow = game.shadow.arrayToShadow[row];
        if ( shadowRow.type == Shadow.DeltaType.Uint8 ) {
            byte [] zero = new byte[row.Length];
            if ( Delta.DeltaBytes( ( byte[] )row, zero, out string changes, out string values ) ) {
                storage += emitRow( shadowRow.name, changes, values );
            }
        } else if ( shadowRow.type == Shadow.DeltaType.Uint16 ) {
            ushort [] zero = new ushort[row.Length];
            if ( Delta.DeltaShorts( ( ushort[] )row, zero,
                                                        out string changes, out string values ) ) {
                storage += emitRow( shadowRow.name, changes, values );
            }
        } else if ( shadowRow.type == Shadow.DeltaType.Int32 ) {
            int [] zero = new int[row.Length];
            if ( Delta.DeltaInts( ( int[] )row, zero, out string changes, out string values ) ) {
                storage += emitRow( shadowRow.name, changes, values );
            }
        }
    }
    storage += ";";
    try {
        File.WriteAllText( path, storage );
        Log( $"Saved '{path}'" );
    } catch ( Exception ) {
        Error( $"Failed to save '{path}'" );
    }
}

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
    string [] cpargv = new string[argv.Length - 1];
    Array.Copy( argv, 1, cpargv, 0, cpargv.Length );
    if ( Array.IndexOf( cpargv, argv[0] ) >= 0 ) {
        Error( "{argv[0]}: Can't be recursive." );
        return;
    }
    Cellophane.TryExecute( cpargv, zport );
}

static void SvSetTerrain_kmd( string [] argv, int zport ) {
    if ( argv.Length < 4 ) {
        Log( $"Usage: {argv[0]} <x> <y> <terrain>" );
        return;
    }

    int.TryParse( argv[1], out int x );
    int.TryParse( argv[2], out int y );
    int.TryParse( argv[3], out int terrain );

    game.SetTerrain( x, y, terrain );
}

static void SvAddZonePoint_kmd( string [] argv, int zport ) {
    if ( argv.Length < 5 ) {
        Log( $"Usage: {argv[0]} <x> <y> <zone_id> <team> ; zone_id == 0 -- remove zone" );
        return;
    }

    int.TryParse( argv[1], out int x );
    int.TryParse( argv[2], out int y );
    int.TryParse( argv[3], out int id );
    int.TryParse( argv[4], out int team );

    game.SetZonePoint( x, y, id: id, team: team );
    Log( $"Adding zone point for zone {id} on hex {game.board.Hex( x, y )}; team: {team}." );
}

static void SvSetZonePoint_kmd( string [] argv, int zport ) {
    if ( argv.Length < 6 ) {
        Log( $"Usage: {argv[0]} <x> <y> <zone_id> <team> <polyIdx>; zone_id == 0 -- remove zone" );
        return;
    }

    int.TryParse( argv[1], out int x );
    int.TryParse( argv[2], out int y );
    int.TryParse( argv[3], out int id );
    int.TryParse( argv[4], out int team );
    int.TryParse( argv[5], out int polyIdx );

    game.SetZonePoint( x, y, id: id, team: team, polyIdx: polyIdx );
    Log( $"Setting zone point for zone {id} on hex {game.board.Hex( x, y )}; team: {team}, index: {polyIdx}." );
}

static void SvPrintClients_kmd( string [] argv ) {
    foreach ( var c in ZServer.clients ) {
        Log( $"endpoint: {c.endPoint}; zport: {c.netChan.zport}" ); 
    }
}

static void SvUndelta_kmd( string [] argv ) {
    if ( argv.Length < 3 ) {
        Log( "Nothing to undelta." );
        return;
    }

    string [] cpargv = new string[argv.Length - 1];
    Array.Copy( argv, 1, cpargv, 0, cpargv.Length );
    if ( game.UndeltaState( cpargv, 0, out bool updateBoard ) && updateBoard ) {
        foreach ( var hx in game.board.filter.spawners ) {
            Vector2 v = game.HexToV( hx );
            if ( game.Spawn( game.board.pawnDef[hx], v.x, v.y, out int z ) ) {
                game.pawn.team[z] = game.board.pawnTeam[hx];
            }
        }
    }
}

static void Spawn( string [] argv, int zport, int def ) {
    float x = Cellophane.AtoF( argv[2] );
    float y = Cellophane.AtoF( argv[3] );
    game.Spawn( def, x, y, out int z );
    if ( argv.Length > 4 ) {
        int.TryParse( argv[4], out int team );
        game.SetTeam( z, team );
    } else {
        int pl = game.player.GetByZPort( zport );
        game.SetTeam( z, game.player.team[pl] );
    }
}

static void SvSpawn_kmd( string [] argv, int zport ) {
    if ( argv.Length < 4 ) {
        Error( $"{argv[0]} <def_name> <x> <y> [team]" );
        return;
    }
    if ( ! Pawn.FindDefIdxByName( argv[1], out int def ) ) {
        Error( $"{argv[0]} Can't find def named {argv[1]}" );
        return;
    }
    int cost = Pawn.defs[def].cost;
    if ( ! game.player.ConsumeMana( game.player.GetByZPort( zport ),
                                                            amount: cost, clock: ZServer.clock ) ) {
        Log( $"Can't spawn, not enough mana." );
        return;
    }
    Spawn( argv, zport: zport, def: def );
}

// ignores mana cost
static void SvForcedSpawn_kmd( string [] argv, int zport ) {
    if ( argv.Length < 4 ) {
        Error( $"{argv[0]} <def_name> <x> <y> [team]" );
        return;
    }
    if ( ! Pawn.FindDefIdxByName( argv[1], out int def ) ) {
        Error( $"{argv[0]} Can't find def named {argv[1]}" );
        return;
    }
    Spawn( argv, zport: zport, def: def );
}

static void SvKill_kmd( string [] argv, int zport ) {
    if ( argv.Length < 2 ) {
        Error( $"{argv[0]} <z>" );
        return;
    }
    int.TryParse( argv[1], out int z );
    game.Destroy( z );
}

static void SvSetTeam_kmd( string [] argv, int zport ) {
    if ( argv.Length < 3 ) {
        Error( $"{argv[0]} <z> <team>" );
        return;
    }
    int.TryParse( argv[1], out int z );
    int.TryParse( argv[2], out int team );
    game.SetTeam( z, team );
}


}
