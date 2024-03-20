using System;
using System.Net;

public static class StandaloneServer {

static void Main( string[] args ) {
    const int PULSE_TIME = 3333;
    const int CONFIG_VERSION = 2;

    Qonsole.Init( CONFIG_VERSION );

    Game game = new Game();

    int pulseTime = PULSE_TIME;
    int traceLevel = 0;

    for ( int i = 0; i < args.Length; i++ ) {
        if ( args[i] == "--test_drop_packets" ) {
            Net.TestDropPackets_cvar = 64;
        } else if ( args[i] == "--trace_level" && i < args.Length - 1 ) {
            int.TryParse( args[i + 1], out traceLevel );
        }
    }

    try {

    if ( ! RRServer.Init( svh: "RadicalRumbleServer: ", logTimestamps: true ) ) {
        ZServer.Error( "Failed to initialize standalone Radical Rumble server, quit." );
        return;
    }

    Cellophane.Log = RRServer.Log;
    Cellophane.Error = RRServer.Error;

    ZServer.onExit_f = Qonsole.FlushConfig;

    ZServer.Log( $"Starting Radical Rumble server, test_drop_packets: {Net.TestDropPackets_cvar}" );
    ZServer.Log( $"Receive Buffer Size: {ZServer.net.socket.ReceiveBufferSize}" );
    ZServer.Log( $"Send Buffer Size: {ZServer.net.socket.SendBufferSize}" );


    while ( true ) { try { 
        // force delta generation/send i.e. when reliable command needs to be ACK-ed back
        // or sending the pulse
        bool forcePacket = false;

        // while idling, drain incoming packets and send out any pending packets
        // if got client command, stop idling and shoot a delta immediately

        const int maxTick = 80;

        int tick = maxTick;
        DateTime prevTime = DateTime.UtcNow;
        while ( tick > 0 ) {
            // sleep for that many microseconds if no activity
            ZServer.Poll( out bool hadCommands, microseconds: 20 * 1000 );
            if ( hadCommands ) {
                // generate delta when any client command got executed on the server
                // and send the delta immediately
                forcePacket = true;
                break;
            }
            DateTime now = DateTime.UtcNow;
            int dt = ( int )( now - prevTime ).TotalMilliseconds;
            tick -= dt;
            prevTime = now;
            pulseTime -= dt;
        }

        if ( pulseTime < 0 ) {
            forcePacket = true;
        }

        ZServer.TickWithClocks( forcePacket );

        if ( forcePacket ) {
            pulseTime = PULSE_TIME;
        }

    } catch ( Exception e ) {
        ZServer.Error( e.ToString() );
    } }

    } catch ( Exception e ) {
        ZServer.Error( e.ToString() );
    } finally {
        ZServer.Done();
        Qonsole.FlushConfig();
    }

    ZServer.Log( "Radical Rumble server stopped." );
    Qonsole.FlushConfig();
}


}
