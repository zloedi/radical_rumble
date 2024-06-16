using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
using SDLPorts;
#endif

namespace RR {
    

using Cl = RR.Client;
using Trig = RR.Pawn.ClientTrigger;
using PDF = RR.Pawn.Def.Flags;

    
public static class ClientPlayUnity {

static bool _initialized;

static class Model {
    public static GameObject Dummy = new GameObject( "__DUMMY__" );
    public static GameObject Archer = Dummy;
}

public static void Tick() {
    if ( ! _initialized ) {
        FieldInfo [] fields = typeof( Model ).GetFields( Cellophane.BFS );
        foreach ( FieldInfo fi in fields ) {
            if ( fi.Name == "Dummy" ) {
                continue;
            }
            var go = UnityLoad( $"mdl_{Cellophane.NormalizeName( fi.Name )}" ) as GameObject;
            if ( go ) {
                fi.SetValue( null, go );
            }
        }
        //Qonsole.QonInvertPlayY_kvar = false;
        _initialized = true;
    }
}

static UnityEngine.Object UnityLoad( string name ) {
    UnityEngine.Object result = Resources.Load( name );
    if ( ! result ) {
        Cl.Error( $"Failed to load '{name}'" );
        return null;
    }
    Cl.Log( $"Loaded '{name}'" );
    return result;
}


} }
