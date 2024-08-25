#if UNITY_STANDALONE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RR {
    

using Cl = RR.Client;
using Trig = RR.Pawn.ClientTrigger;
using PDF = RR.Pawn.Def.Flags;

    
public static class GUIUnity {


[Description( "Show healthbars: 0 -- show damaged healthbars, 1 -- show all healthbars, 2 -- don't show any healthbars." )]
static int GuiShowHealthbars_kvar = 0;

[Description( "The mana bar stretches" )]
static bool GuiManabarStretch_kvar = false;

public static int localTeam = 0;
public static float localMana = 0;
public static bool isObserver = false;

public static Font font;

public static class prefab {
    public static GameObject Window;
    public static GameObject Panel;
    public static GameObject SliderScroll;
    public static GameObject ManaBar;
    public static GameObject [] HealthBarSz = new GameObject[Pawn.Def.MAX_HEALTH_BAR];
}

static GameObject _dummyPrefab;

static Pawn _pawn => Cl.game.pawn;

public static void Init()
{
    _dummyPrefab = new GameObject( "__GUI_DUMMY_PREFAB__" );
    _dummyPrefab.AddComponent<Image>();
    _dummyPrefab.AddComponent<Slider>();
    _dummyPrefab.AddComponent<TMP_Text>();
    LoadPrefabs();
    font = UnityLoad( "Alata-Regular" ) as Font;
}

public static void DrawHealthBars() {
    if ( GuiShowHealthbars_kvar >= 2 ) {
        return;
    }
         
    if ( ! Camera.main ) {
        return;
    }

    float scale = Screen.height / 1800f;

    string [] refChildren = {
        "gui_slider",
        "gui_fill",
    };

    foreach ( var sizeByTeam in _pawn.filter.healthbar ) {
        for ( int size = 0; size < sizeByTeam.Length; size++ ) {
            var pawns = sizeByTeam[size];
            foreach ( var z in pawns ) {
                // FIXME: what if we have dead that are not hp
                if ( _pawn.hp[z] == 0 ) {
                    continue;
                }

                if ( GuiShowHealthbars_kvar == 0 && _pawn.hp[z] == _pawn.MaxHP( z ) ) {
                    continue;
                }

                Color c = _pawn.team[z] == localTeam ? new Color( 0, 0.45f, 1f ) : Color.red;
                Vector2 posGame = _pawn.mvPos[z];
                Vector3 posWorld = new Vector3( posGame.x, 0, posGame.y );
                posWorld += _pawn.GetDef( z ).u_healthbarOffset;
                // FIXME: make a QUI analog
                Vector2 pt = Camera.main.WorldToScreenPoint( posWorld );
                pt.y = Screen.height - pt.y;
                int def = _pawn.def[z];
                RectTransform [] children = QUI.Prefab( pt.x, pt.y, scale: scale,
                                                prefab: prefab.HealthBarSz[size],
                                                refChildren: refChildren, handle: def << 16 | z );
                Child<Slider>( children, 0 ).value = _pawn.hp[z] / ( float )_pawn.MaxHP( z );
                Child<Image>( children, 1 ).color = c;
            }
        }
    }
}

public static void DrawManaBar( WrapBox wbScreen ) {
    //if ( isObserver ) {
    //    return;
    //}

    string [] refChildren = {
        "gui_slider",
        "gui_fill",
        "gui_text",
    };

    RectTransform [] children;

    if ( GuiManabarStretch_kvar ) {
        children = QUI.Prefab( 0, Screen.height - 60,
                                                rtW: Screen.width, rtH: 48, prefab: prefab.ManaBar,
                                                refChildren: refChildren );
    } else {
        var wb = wbScreen.BottomCenter( 1000, 200 );
        wb = wb.Center( wb.W, wb.H / 2 );
        children = QUI.PrefabScaled( wb.x, wb.y, wb.w, wb.h, prefab: prefab.ManaBar,
                                                                        refChildren: refChildren );
    }

    Child<Slider>( children, 0 ).value = localMana / 10f;
    float bump = 0.2f * ( 1 + 0.5f * Mathf.Sin( Time.time * 3 ) );
    Child<Image>( children, 1 ).color = new Color( 0.6f, 0.1f, 0.6f ) + new Color( 1, 1, 1 ) * bump;
    Child<TMP_Text>( children, 2 ).text = ( ( int )localMana ).ToString();
}

public static T Child<T>( RectTransform [] children, int child ) {
    var dummy = _dummyPrefab.GetComponent<T>();
    var comp = children[child].GetComponent<T>();
    return comp != null ? comp : dummy;
}

static void LoadPrefabs() {
    FieldInfo [] fields = typeof( GUIUnity.prefab ).GetFields();
    foreach ( FieldInfo fi in fields ) {
        if( fi.FieldType.IsArray ) {
            var array = ( GameObject [] )fi.GetValue( null );
            for ( int i = 0; i < array.Length; i++ ) {
                var go = UnityLoad( $"gui_{Cellophane.NormalizeName( fi.Name )}_{i}" ) as GameObject;
                array[i] = go ? go : _dummyPrefab;
            }
        } else {
            var go = UnityLoad( $"gui_{Cellophane.NormalizeName( fi.Name )}" ) as GameObject;
            fi.SetValue( null, go ? go : _dummyPrefab );
        }
    }
}

static UnityEngine.Object UnityLoad( string name ) {
    UnityEngine.Object result = Resources.Load( name );
    if ( ! result ) {
        Cl.Error( $"GUI: Failed to load '{name}'" );
        return null;
    }
    Cl.Log( $"GUI: Loaded '{name}'" );
    return result;
}


} // GUIUnity


} // RR

#endif
