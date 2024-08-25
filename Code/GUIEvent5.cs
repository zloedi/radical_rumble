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


static Pawn _pawn => Cl.game.pawn;

static List<List<byte>> _collections = new List<List<byte>>();
static int _collection;

public static void Tick_ui( WrapBox wbox ) {

    if ( _collections.Count < _numCollections ) {
        for ( int i = _collections.Count; i < _numCollections; i++ ) {
            _collections.Add( new List<byte>() );
        }
    }

    if ( _collections.Count > _numCollections ) {
        for ( int i = _collections[_collections.Count - 1].Count - 1; i >= 1; i-- ) {
            var colSrc = _collections[i - 0];
            var colDst = _collections[i - 1];
            foreach (var z in colSrc) {
                colDst.Add( z );
            }
            _collections.RemoveAt( i );
        }
    }

    wbox = Window_ui( wbox );

    for ( wbox = wbox.TopLeft( _panelSize, wbox.H ), _collection = 0;
            _collection < _collections.Count; wbox = wbox.NextRight(), _collection++ ) {
        Panel_ui( wbox );
    }
}

static WrapBox Window_ui( WrapBox wbox ) {
    wbox = wbox.TopCenter( _numCollections * _panelSize, wbox.H - 320, y: 80, id: _collection );
    var wboxFrame = wbox.Center( wbox.W + 100, wbox.H + 100, id: _collection );
    string [] refChildren = { "gui_text", };
    RectTransform [] children = QUI.PrefabScaled( wboxFrame.x, wboxFrame.y,
                                                wboxFrame.w, wboxFrame.h,
                                                rtW: wboxFrame.W, rtH: wboxFrame.H,
                                                prefab: GUIUnity.prefab.Window,
                                                refChildren: refChildren );
    Child<TMP_Text>( children, 0 ).text = $"Window Title "
                                        + $"Mana: {localMana:00.00}; "
                                        + $"Clock: {Mathf.Repeat( Cl.clock/1000f, 20 ):00.00}";

    return wbox;
}

static void Panel_ui( WrapBox wbox ) {
    WrapBox wbSlider = Slider_ui( wbox, out float sliderValue );
    //WrapBox wbList = wbox.TopLeft( wbox.W - wbSlider.W, wbox.H );
    //List_ui( wbList, _collectionA, slider );
}

static WrapBox Slider_ui( WrapBox wbox, out float sliderValue ) {
    wbox = wbox.BottomRight( 20, wbox.H * 0.95f );
    WBUI.ClickRect( wbox );
    sliderValue = 0;
    return wbox;
}

//static void List_ui(


} // GuiEvent5


} // RR

#endif
