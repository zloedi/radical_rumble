using System;
using System.Collections.Generic;
using System.ComponentModel;

static class RRClient {


[Description( @"Connect to this server for multiplayer.
// 127.0.0.1 -- this is a local game and the host is this machine
// 89.190.193.149 -- raspberry pi server."
)]
public static string ClServerIpAddress_kvar = "89.190.193.149";

[Description( "0 -- minimal network logging, 1 -- some network logging, 2 -- detailed network logging, 3 -- full network logging " )]
public static int ClTraceLevel_kvar = 1;

[Description("Print incoming packets on game ReceivePacket(): 1 -- some; 2 -- all")]
static int ClPrintIncomingPackets_kvar = 0;

public static Game game = new Game();

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

public static void Tick( int timeDeltaMs ) {
    ZClient.Tick( timeDeltaMs );
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
        game.Log = s => Qonsole.Log( $"[00C0FF]Client: [-] {s}" );
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

static List<ushort> deltaChange = new List<ushort>();
static List<int> deltaNumbers = new List<int>();
public static bool UndeltaGameState( string [] argv ) {
    if ( argv.Length < 1 ) {
        return false;
    }

    bool result = false;

    for ( int idx = 0; idx < argv.Length; ) {
        string rowName = argv[idx++];

        if ( ! game.shadow.nameToArray.TryGetValue( rowName, out Array row ) ) {
            Error( $"Undelta: Can't find {rowName} in row names." );
            continue;
        }

        if ( ! game.shadow.arrayToShadow.TryGetValue( row, out Shadow.Row shadowRow ) ) {
            Error( $"Can't find {rowName} in shadows." );
            continue;
        }

        if ( Delta.UndeltaNum( ref idx, argv, deltaChange, deltaNumbers, out bool keepGoing ) ) {
            result = true;
            if ( shadowRow.type == Shadow.DeltaType.Uint8 ) {
                for ( int i = 0; i < deltaChange.Count; i++ ) {
                    ( ( byte [] )row )[deltaChange[i]] = ( byte )deltaNumbers[i];
                }
            } else if ( shadowRow.type == Shadow.DeltaType.Uint16 ) {
                for ( int i = 0; i < deltaChange.Count; i++ ) {
                    ( ( ushort [] )row )[deltaChange[i]] = ( ushort )deltaNumbers[i];
                }
            } else if ( shadowRow.type == Shadow.DeltaType.Int32 ) {
                for ( int i = 0; i < deltaChange.Count; i++ ) {
                    ( ( int [] )row )[deltaChange[i]] = ( int )deltaNumbers[i];
                }
            }
        }

        if ( ! keepGoing ) {
            break;
        }
    }

#if false
    if ( persist && argv.Length > 1 && board.numItems > 0 ) {
#if UNITY_STANDALONE
        List<ushort> list = new List<ushort>();
        for ( int i = 0; i < board.numItems; i++ ) {
            if ( board.terrain[i] != 0 ) {
                list.Add( ( ushort )i );
            }
        }
        Hexes.PrintList( list, board.width, board.height, logText: "Undelta Board grid",
                                        hexListString: (l,i) => $"{l[i].x},{l[i].y}", hexSize: 48 );
#endif
    }
#endif

    return result;
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

    if ( ! Cellophane.GetArgvBare( deltaCmd, out string [] argv ) ) {
        Error( "Expected game delta leading in the packet. Dropping the packet." );
        return;
    }

    // apply server game state on the client
    UndeltaGameState( argv );

    if ( ClPrintIncomingPackets_kvar == 2 ) {
        Log( $"incoming packet: '{packetStr}'" );
    }

    // maybe there are some trailing commands after the delta, try to execute them here
    string trailingCommands = packetStr.Substring( deltaNumChars ).TrimStart();

    // really messes up when there is 'echo' inside 'echo'
    // i.e. when server is set to bounce logs and the bounce is logged with the packet
    Cellophane.SplitCommands( trailingCommands, out string [] cmds );
    foreach ( var c in cmds ) {
        Execute( c );
    }
}


}
