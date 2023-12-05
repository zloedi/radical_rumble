using System;
using System.Reflection;

static class TickUtil {

public static Action<string> Log = s => {};
public static Action<string> Error = s => {};

public static Action [] RegisterTicks( Type type, out string [] tickNames, params Action [] ticks ) {
    tickNames = new string[ticks.Length];
    MethodInfo [] methods = type.GetMethods( Cellophane.BFS );
    foreach ( MethodInfo mi in methods ) {
        if ( mi.Name.EndsWith( "_tck" ) ) {
            for ( int i = 0; i < ticks.Length; i++ ) {
                if ( ticks[i].GetHashCode() == mi.GetHashCode() ) {
                    var nm = mi.Name.Remove( mi.Name.Length - 4 );
                    tickNames[i] = Cellophane.NormalizeName( nm );
                }
            }
        }
    }
    return ticks;
}

public static bool SetState( string [] argv, Action [] ticks, string [] tickNames, ref int state ) {
    int idx;
    if ( argv.Length < 2 || ( idx = Array.IndexOf( tickNames, argv[1] ) ) < 0 ) {
        foreach ( var n in tickNames ) {
            Log( n );
        }
        Log( $"{argv[0]} <state_name>" );
        return false;
    }
    state = idx;
    Log( $"Setting state to {argv[1]}" );
    return true;
}


}
