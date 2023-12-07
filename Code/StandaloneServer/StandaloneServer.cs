using System;
using System.Net;

using static GameConstants;

public static class StandaloneServer {

static void Main( string[] args ) {
    Qonsole.Init( CONFIG_VERSION );

    Game game = new Game();

    const int PULSE_TIME = 3333;

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

    if ( ! Server.Init( svh: "WurstServer: ", timestamps: true ) ) {
        ZServer.Error( "Failed to initialize standalone Wurst server, quit." );
        return;
    }

    Cellophane.Log = Server.Log;
    Cellophane.Error = Server.Error;

    ZServer.onExit_f = Qonsole.FlushConfig;

    ZServer.Log( $"Starting Wurst server, test_drop_packets: {Net.TestDropPackets_cvar}" );
    ZServer.Log( $"Receive Buffer Size: {ZServer.net.socket.ReceiveBufferSize}" );
    ZServer.Log( $"Send Buffer Size: {ZServer.net.socket.SendBufferSize}" );


    while ( true ) { try { 
        // force delta generation/send i.e. when reliable command needs to be ACK-ed back
        // or sending the pulse
        bool forcePacket = false;

        // while idling, drain incoming packets and send out any pending packets
        // if got client command, stop idling and shoot a delta immediately

        const int maxTick = 256;

        int tick = maxTick;
        DateTime prevTime = DateTime.UtcNow;
        while ( tick > 0 ) {
            // sleep for that many microseconds if no activity
            // will execute any incoming client reliable commands immediately
            ZServer.Poll( out bool hadCommands, microseconds: 64 * 1024 );
            if ( hadCommands ) {
                forcePacket = true;
                break;
            }
            DateTime now = DateTime.UtcNow;
            int dt = ( int )( now - prevTime ).TotalMilliseconds;
            tick -= dt;
            prevTime = now;
            pulseTime -= dt;
            Common.timeDeltaMs = dt;
            Common.timeDeltaSec = dt * 1000f;
            Common.timeNow += dt;
        }

        if ( pulseTime < 0 ) {
            forcePacket = true;
        }

        ZServer.Tick( maxTick - tick, forcePacket );

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

    ZServer.Log( "Wurst server stopped." );
    Qonsole.FlushConfig();
}


}
