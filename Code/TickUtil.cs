using System;
using System.Collections.Generic;
using System.Reflection;

static class TickUtil {


public static Action<string> Log = s => {};
public static Action<string> Error = s => {};

public static Action [] RegisterTicksOfClass( Type type, out string [] tickNames ) {
    var ticks = new List<Action>();
    var names = new List<string>();
    MethodInfo [] methods = type.GetMethods( Cellophane.BFS );
    foreach ( MethodInfo mi in methods ) {
        if ( mi.Name.EndsWith( "_tck" ) ) {
            var nm = mi.Name.Remove( mi.Name.Length - 4 );
            ticks.Add( mi.CreateDelegate( typeof( Action ) ) as Action );
            names.Add( Cellophane.NormalizeName( nm ) );
            Log( $"Registered tick {names[names.Count - 1]}" );
        }
    }
    tickNames = names.ToArray();
    return ticks.ToArray();
}

public static bool SetState( string [] argv, Action [] ticks, string [] tickNames, ref int state ) {
    int idx;
    if ( argv.Length < 2 || ( idx = Array.IndexOf( tickNames, argv[1] ) ) < 0 ) {
        int i = 0;
        foreach ( var n in tickNames ) {
            Log( $"{i++}: {n}" );
        }
        Log( $"{argv[0]} <state_name>" );
        return false;
    }
    state = idx;
    Log( $"Setting state to {argv[1]}" );
    return true;
}


}
