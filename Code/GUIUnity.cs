#if UNITY_STANDALONE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace RR {
    

using Cl = RR.Client;
using Trig = RR.Pawn.ClientTrigger;
using PDF = RR.Pawn.Def.Flags;

    
public static class GUIUnity {


public static class prefab {
    // FIXME: put them into an array
    public static GameObject HealthBarSz0;
    public static GameObject HealthBarSz1;
    public static GameObject HealthBarSz2;
    public static GameObject HealthBarSz3;
}

static GameObject _dummyPrefab;

static Pawn _pawn => Cl.game.pawn;

public static void Init()
{
    _dummyPrefab = new GameObject( "__GUI_DUMMY_PREFAB__" );

    FieldInfo [] fields = typeof( GUIUnity.prefab ).GetFields();
    foreach ( FieldInfo fi in fields ) {
        fi.SetValue( null, _dummyPrefab );
    }

    LoadAssets();
}

public static void TickHealthBars() {
    if ( ! Camera.main ) {
        return;
    }

    foreach ( var sizeByTeam in _pawn.filter.healthbar ) {
        for ( int size = 0; size < sizeByTeam.Length; size++ ) {
            var pawns = sizeByTeam[size];
            foreach ( var z in pawns ) {
                Vector2 posGame = _pawn.mvPos[z];
                Vector3 posWorld = new Vector3( posGame.x, 0, posGame.y );

                // FIXME: make a QUI analog
                Vector2 pt = Camera.main.WorldToScreenPoint( posWorld );
                pt.y = QGL.ScreenHeight() - pt.y;

                QGL.LatePrint( size, pt );
                QUI.Prefab( pt.x, pt.y, prefab: prefab.HealthBarSz1, handle: z );
            }
        }
    }
}

static void LoadAssets() {
    FieldInfo [] fields = typeof( GUIUnity.prefab ).GetFields();
    foreach ( FieldInfo fi in fields ) {
        var go = UnityLoad( $"gui_{Cellophane.NormalizeName( fi.Name )}" ) as GameObject;
        if ( go ) {
            fi.SetValue( null, go );
        }
    }
}

static UnityEngine.Object UnityLoad( string name ) {
    UnityEngine.Object result = Resources.Load( name );
    if ( ! result ) {
        Cl.Error( $"[GUI] Failed to load '{name}'" );
        return null;
    }
    Cl.Log( $"[GUI] Loaded '{name}'" );
    return result;
}

} // GUIUnity


} // RR

#endif
