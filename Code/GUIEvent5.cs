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
using static QUI.WidgetResult;

public static class GuiEvent5 {


static int NumCollections_cvar = 2;
static int _numCollections => Mathf.Clamp( NumCollections_cvar, 2, 8 );

static int PanelSize_cvar = 400;
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
static List<float> _sliders = new List<float>();
static float _slider { get => _sliders[_collectionIdx]; set { _sliders[_collectionIdx] = value; } }

// TODO: draggable main window
// TODO: collapse main window

public static void Tick_ui( WrapBox wbox ) {
    if ( _collections.Count == 0 ) {
        _collections.Add( new List<byte>() );
        _sliders.Add( 0 );
        for ( int z = 0; z < Pawn.MAX_PAWN; z++ ) {
            _collections[0].Add( ( byte )z );
        }
    }

    if ( _collections.Count < _numCollections ) {
        for ( int i = _collections.Count; i < _numCollections; i++ ) {
            _collections.Add( new List<byte>() );
            _sliders.Add( 0 );
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
            _sliders.RemoveAt( i );
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
}

static void List_ui( WrapBox wbox ) {
    wbox = wbox.TopCenter( wbox.W - 50, wbox.H - 80 - 60, y: 90 );
    var wboxScissor = wbox.TopLeft( wbox.W - 20, wbox.H );
    var wboxList = wbox.TopLeft( wboxScissor.W, wbox.H + _slider, y: -_slider );

    WBUI.ClickRect( wboxList );

    float y = 0;

    WBUI.EnableScissor( wboxScissor );
    for ( int i = 0; i < Mathf.Min( 32, _collection.Count ); i++ ) {
        ListItem_ui( wboxList, i, ref y );
    }
    WBUI.DisableScissor();

    Slider_ui( wbox, y );
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
        var wboxText = wbox.TopLeft( wbox.W, wbox.H, y: textY );
        int handle = WBUI.MeasuredText( contents, wboxText, out float measureW, out float measureH,
                                            font: GUIUnity.font, fontSize: -1, handle: element );
        wboxText = wboxText.TopLeft( wboxText.W, measureH );
        WBUI.ClickRect( wboxText );
        WBUI.Text_wg( contents, wboxText, font: GUIUnity.font, fontSize: -1, handle: handle );

        element++;

        return measureH;
    }
}

static void Slider_ui( WrapBox wbox, float total ) {
    var wboxSlider = wbox.TopRight( 20, wbox.H );

    string [] refChildren = { "gui_slider", "gui_handle" };

    RectTransform [] children = QUI.PrefabScaled( wboxSlider.x, wboxSlider.y,
                                                wboxSlider.w, wboxSlider.h,
                                                rtW: wboxSlider.W, rtH: wboxSlider.H,
                                                prefab: GUIUnity.prefab.SliderScroll,
                                                refChildren: refChildren,
                                                handle: wbox.id );
    var slider = Child<Slider>( children, 0 );
    var rt = Child<RectTransform>( children, 1 );
    var img = Child<Image>( children, 1 );

    var wboxHandle = WBUI.FromRectTransform( rt );
    var clickResult = WBUI.ClickRect( wboxHandle );
    if ( clickResult != Idle ) {
        img.color = Color.white;
        if ( clickResult == Active ) {
            float n = wboxSlider.h;
            float s = Mathf.Clamp( ( QUI.cursorY - wboxSlider.y ) / wboxSlider.h, 0, 1 );
            _slider = s * ( total - wbox.H );
            slider.value = 1 - s;
        }
    } else {
        img.color = new Color( 0.75f, 0.75f, 0.75f );
    }
}


} // GuiEvent5


} // RR

#endif
