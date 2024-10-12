using System;
using System.Reflection;

#if UNITY_STANDALONE
using UnityEngine;
#else
using SDLPorts;
using GalliumMath;
#endif

namespace RR {


static class Recompile {


#if ! ROSLYN 
public static void Init() {}
public static void Tick() {}
public static void Done() {}
#else

static bool _initialized;

public static void Init() {
    HotRoslyn.Log = o => Qonsole.Log( "Roslyn: " + o );
    HotRoslyn.Error = s => Qonsole.Error( "Roslyn: " + s );
    HotRoslyn.OnCompile = OnCompile;

    // here roslyn will scan for file changes, including subdirs
    HotRoslyn.ScriptsRoot = $"{Application.dataPath}/../../Code/";

    // source files to recompile
    HotRoslyn.ScriptFiles = new [] {
        "ClientPlayUnity.cs",
        "GUIEvent5.cs",
        "GUIEvent6.cs",
        "GUIUnity.cs",
    };

    if ( ! HotRoslyn.TryInit() ) {
        return;
    }

    _initialized = true;
}

public static void Tick() {
    if ( ! _initialized ) {
        return;
    }
    HotRoslyn.Update();
}

public static void Done() {
    HotRoslyn.Done();
}

static void OnCompile( Assembly assembly ) {
    Cellophane.ImportAndReplace( assembly );
    Type t = assembly.GetType( "RR.ClientPlayUnity" );
    MethodInfo mi = t.GetMethod( "Tick" );
    Client.OverrideTick( "play", () => {
        try {
            mi.Invoke( null, null );
        } catch ( Exception e ) {
            Qonsole.Error( e );
            Client.OverrideTick( "play", ClientPlayUnity.Tick );
        }
    } );
}

#endif // ROSLYN


}


}
