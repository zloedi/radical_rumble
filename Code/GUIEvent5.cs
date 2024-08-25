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
using prefab = GUIUnity.prefab;

using static GUIUnity;

public static class GuiEvent5 {


static int NumCollections_cvar = 2;
static int _numCollections => Mathf.Clamp( NumCollections_cvar, 2, 8 );

static int PanelSize_cvar = 200;
static int _panelSize => Mathf.Clamp( PanelSize_cvar, 20, 500 );

static int SizeTitle_cvar = 30;
static string ColorTitle_cvar = "#ffffff";

static int SizeText_cvar = 18;
static string ColorText_cvar = "#c0c0c0";

//static float Test_cvar = 0;

static Pawn _pawn => Cl.game.pawn;

static List<List<byte>> _collections = new List<List<byte>>();
static int _collectionIdx;
static List<byte> _collection => _collections[_collectionIdx];

// TODO: draggable main window
// TODO: collapse main window

public static void Tick_ui( WrapBox wbox ) {
    if ( _collections.Count == 0 ) {
        _collections.Add( new List<byte>() );
        for ( int z = 0; z < Pawn.MAX_PAWN; z++ ) {
            _collections[0].Add( ( byte )z );
        }
    }

    if ( _collections.Count < _numCollections ) {
        for ( int i = _collections.Count; i < _numCollections; i++ ) {
            _collections.Add( new List<byte>() );
        }
    }

    if ( _collections.Count > _numCollections ) {
        for ( int i = _collections.Count - 1; i >= _numCollections; i-- ) {
            var colSrc = _collections[i - 0];
            var colDst = _collections[i - 1];
            foreach (var z in colSrc) {
                colDst.Add( z );
            }
            _collections.RemoveAt( i );
        }
    }

    Window_ui( wbox );
}

static void Window_ui( WrapBox wbox ) {
    wbox = wbox.TopCenter( _numCollections * _panelSize, wbox.H - 320, y: 80 );
    var wboxFrame = wbox.Center( wbox.W + 100, wbox.H + 100 );
    string [] refChildren = { "gui_text", };
    RectTransform [] children = QUI.PrefabScaled( wboxFrame.x, wboxFrame.y,
                                                wboxFrame.w, wboxFrame.h,
                                                rtW: wboxFrame.W, rtH: wboxFrame.H,
                                                prefab: GUIUnity.prefab.Window,
                                                refChildren: refChildren );

    Child<TMP_Text>( children, 0 ).text = $"Window Title "
                                        + $"Mana: {localMana:00.00}; "
                                        + $"Clock: {Mathf.Repeat( Cl.clock/1000f, 20 ):00.00}";

    for ( wbox = wbox.TopLeft( _panelSize, wbox.H ), _collectionIdx = 0;
            _collectionIdx < _numCollections;
            _collectionIdx++, wbox = wbox.NextRight( _collectionIdx ) ) {
        Panel_ui( wbox );
    }
}

static void Panel_ui( WrapBox wbox ) {
    wbox = wbox.BottomLeft( wbox.W - 10, wbox.H - 60 );
    string [] refChildren = { "gui_text", };
    RectTransform [] children = QUI.PrefabScaled( wbox.x, wbox.y,
                                                wbox.w, wbox.h,
                                                rtW: wbox.W, rtH: wbox.H,
                                                prefab: GUIUnity.prefab.Panel,
                                                refChildren: refChildren,
                                                handle: wbox.id );
    Child<TMP_Text>( children, 0 ).text = $"Collection {(char)('A' + _collectionIdx)}";
    List_ui( wbox );
    //WrapBox wbSlider = Slider_ui( wbox, out float sliderValue );
    //WrapBox wbList = wbox.TopLeft( wbox.W - wbSlider.W, wbox.H );
    //List_ui( wbList, _collectionA, slider );
}

static WrapBox Slider_ui( WrapBox wbox, out float sliderValue ) {
    wbox = wbox.TopCenter( wbox.W - 40, wbox.H - 80 - 60, y: 90 );
    wbox = wbox.TopRight( 30, wbox.H );
    WBUI.ClickRect( wbox );
    sliderValue = 0;
    return wbox;
}

static void List_ui( WrapBox wbox ) {
    wbox = wbox.TopCenter( wbox.W - 50, wbox.H - 80 - 60, y: 90 );
    //WBUI.ClickRect( wbox );

    float y = 0;
    for ( int i = 0; i < _collection.Count; i++ ) {
        ListItem_ui( wbox, i, ref y );
    }
}

static void ListItem_ui( WrapBox wbox, int i, ref float y ) {
    int z = _collection[i];
    Pawn.Def def = _pawn.GetDef( z );

    string title = $"<size={WrapBox.ScaleRound( SizeTitle_cvar )}><color={ColorTitle_cvar}>{def.name}</color></size>";
    string text = $"<size={WrapBox.ScaleRound( SizeText_cvar )}><color={ColorText_cvar}>{def.description}</color></size>";

    int element = i * 31;

    y += doText( title, y );
    y += doText( text, y );
    y += SizeText_cvar * 3;

    float doText( string contents, float textY ) {
        WrapBox wboxText = wbox.TopLeft( wbox.W, wbox.H, y: textY );
        int handle = WBUI.MeasuredText( contents, wboxText, out float measureW, out float measureH,
                                            font: GUIUnity.font, fontSize: -1, handle: element );
        WBUI.Text_wg( contents, wboxText, font: GUIUnity.font, fontSize: -1, handle: handle );

        //wboxText = wboxText.TopLeft( wboxText.W, measureH );
        //WBUI.ClickRect( wboxText );

        element++;

        return measureH;
    }
}


} // GuiEvent5


} // RR

#endif
