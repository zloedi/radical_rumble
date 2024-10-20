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


static int NumPanels_cvar = 2;
static int _numPanels => Mathf.Clamp( NumPanels_cvar, 2, 8 );

static int PanelSize_cvar = 400;
static int _panelSize => Mathf.Clamp( PanelSize_cvar, 20, 500 );

static int SizeText_cvar = 18;

static string ColorTitle_cvar = "#ffffff";
static string ColorText_cvar = "#c0c0c0";

//static float Test1_cvar = 0;
//static float Test2_cvar = 0;

static Pawn _pawn => Cl.game.pawn;

static List<List<byte>> _collections = new List<List<byte>>();
static int _collectionIdx;
static List<byte> _collection => _collections[_collectionIdx];
static List<float> _sliders = new List<float>();
static float _slider { get => _sliders[_collectionIdx]; set { _sliders[_collectionIdx] = value; } }

static int _dragItem, _dropItem, _itemUnderCursor;
static int _dragCollectionIdx, _dropCollectionIdx;
static float _dragX, _dragY;
static WrapBox _dragItemBox, _dropItemBox;

static List<byte> _dragCollection => _collections[_dragCollectionIdx];
static List<byte> _dropCollection => _collections[_dropCollectionIdx];

static WrapBox _wboxWindow = new WrapBox{ id = -1 };

// TODO: collapse main window
// TODO: procedural tooltip

public static void Tick_ui( WrapBox wbox ) {
    if ( _collections.Count == 0 ) {
        _collections.Add( new List<byte>() );
        _sliders.Add( 0 );
        for ( int z = 0; z < Pawn.MAX_PAWN; z++ ) {
            _collections[0].Add( ( byte )z );
        }
    }

    if ( _collections.Count < _numPanels ) {
        for ( int i = _collections.Count; i < _numPanels; i++ ) {
            _collections.Add( new List<byte>() );
            _sliders.Add( 0 );
        }
    }

    if ( _collections.Count > _numPanels ) {
        for ( int i = _collections.Count - 1; i >= _numPanels; i-- ) {
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

    if ( _wboxWindow.w != wbox.w || _wboxWindow.h != wbox.h ) {
        _wboxWindow = wbox;
    }
        
    wbox = _wboxWindow.TopCenter( _numPanels * _panelSize, wbox.H - 320, y: 80 );
    var wboxFrame = wbox.Center( wbox.W + 100, wbox.H + 100 );

    var res = WBUI.ClickRect( wboxFrame );
    if ( res == Active ) {
        QUI.DragPosition( res, ref _wboxWindow.x, ref _wboxWindow.y );
    }

    string [] refChildren = { "gui_text", };
    RectTransform [] children = QUI.PrefabScaled( wboxFrame.x, wboxFrame.y,
                                                wboxFrame.w, wboxFrame.h,
                                                rtW: wboxFrame.W, rtH: wboxFrame.H,
                                                prefab: GUIUnity.prefab.Window,
                                                refChildren: refChildren );

    Child<TMP_Text>( children, 0 ).text = $"Window Title "
                                        + $"Mana: {localMana:00.00}; "
                                        + $"Clock: {Mathf.Repeat( Cl.clock/1000f, 20 ):00.00}";

    _dropItem = -1;

    for ( wbox = wbox.TopLeft( _panelSize, wbox.H ), _collectionIdx = 0;
            _collectionIdx < _numPanels;
            _collectionIdx++, wbox = wbox.NextRight( _collectionIdx ) ) {
        Panel_ui( wbox );
    }

    // draw the dragged item on top of everything
    if ( _dragItem >= 0 ) {
        var wboxBgr = _dragItemBox.Center( _dragItemBox.W + 10, _dragItemBox.H + 10 );
        WBUI.FillRect( wboxBgr, color: new Color( 0.15f, 0.1f, 0.2f, 0.75f ) );
        float y = 0;
        _collectionIdx = _dragCollectionIdx;
        ListItemVisuals_ui( _dragItemBox, _dragItem, ref y );
    }

    // handle 'drop'
    if ( _dropItem >= 0 && _dropCollectionIdx >= 0 ) {
        byte z = _dragCollection[_dragItem];

        _dropItem += _dragItemBox.midPoint.y > _dropItemBox.midPoint.y ? 1 : 0;
        _dropItem = Mathf.Clamp( _dropItem, 0, _dropCollection.Count );

        _dragCollection.RemoveAt( _dragItem );
        _dropCollection.Insert( _dropItem, z );

        _dragItem = -1;
        _dropItem = -1;
    }

    if ( _dragItem < 0 && _dropItem < 0 ) {
        _dragCollectionIdx = -1;
        _dropCollectionIdx = -1;
    }

    // just in case if the app got out of focus while dragging
    if ( QUI.activeWidget == 0 ) {
        _dragItem = -1;
    }
}

static void Panel_ui( WrapBox wbox ) {
    wbox = wbox.BottomLeft( wbox.W - 10, wbox.H - 60 );
    // so we don't drag while inside a panel
    WBUI.ClickRect( wbox );
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

    // select list to drop dragged item if dragging
    if ( _dragItem >= 0 && WBUI.CursorInRect( wboxList ) ) {
        WBUI.FillRect( wboxScissor, color: new Color( 1, 1, 1, 0.15f ) );
        _dropCollectionIdx = _collectionIdx;
    }

    // draw the list while scissor
    float y = 0;
    WBUI.EnableScissor( wboxScissor );
    for ( int i = 0; i < Mathf.Min( 32, _collection.Count ); i++ ) {
        ListItem_ui( wboxList, i, ref y );
    }
    WBUI.DisableScissor();

    Slider_ui( wbox, y );
}

static void ListItem_ui( WrapBox wbox, int i, ref float y ) {
    var wboxItem = ListItemVisuals_ui( wbox, i, ref y );

    // keep track of the item under the cursor, we want to insert the dragged there
    if ( _dragItem >= 0 && WBUI.CursorInRect( wboxItem ) ) {
        _itemUnderCursor = i;
        _dropItemBox = wboxItem;
    }

    // handle hover, click and drag input
    var res = WBUI.ClickRect( wboxItem );
    if ( res != Idle ) {
        WBUI.FillRect( wboxItem, color: new Color( 1, 1, 1, 0.15f ) );
        if ( res == Active ) {
            QUI.DragPosition( res, ref _dragX, ref _dragY );
            _dragItemBox = new WrapBox( _dragX, _dragY, wboxItem.w, wboxItem.h, wboxItem.id * 17 ); 
            _dragCollectionIdx = _collectionIdx;
            _dragItem = i;
        } else if ( res == Dropped ) {
            _dragItem = i;
            _dropItem = _itemUnderCursor;
        } else {
            _dragX = wboxItem.x;
            _dragY = wboxItem.y;
        }
    }
}

static WrapBox ListItemVisuals_ui( WrapBox wbox, int i, ref float y ) {
    int z = _collection[i];
    Pawn.Def def = _pawn.GetDef( z );

    string title = $"<size={WrapBox.ScaleRound( SizeText_cvar * 1.6f)}><color={ColorTitle_cvar}>{def.name} {z}</color></size>";
    string text = $"<size={WrapBox.ScaleRound( SizeText_cvar )}><color={ColorText_cvar}>{def.description}</color></size>";

    int childHandle = WBUI.Hash( wbox, i ) * 17;
    float y0 = y;
    y += doText( title, y );
    y += doText( text, y );
    y += SizeText_cvar * 2;

    var wboxItem = wbox.TopLeft( wbox.W, y - y0, y: y0, id: i );

    if ( ! _pawn.IsGarbage( z ) ) {
        string [] refChildren = {
            "gui_slider",
            "gui_fill",
        };

        float hbW = wboxItem.W / 2;
        float hbH = SizeText_cvar * 2f;
        var wboxHB = wboxItem.TopRight( hbW, hbH );
        RectTransform [] children = QUI.Prefab( wboxHB.midPoint.x, wboxHB.midPoint.y,
                                                            scale: wboxHB.h * 0.03f, 
                                                            prefab: prefab.HealthBarSz[1],
                                                            refChildren: refChildren,
                                                            handle: ( _pawn.def[z] << 16 ) | z );
        Color c = _pawn.team[z] == localTeam ? new Color( 0, 0.45f, 1f ) : Color.red;
        Child<Slider>( children, 0 ).value = _pawn.hp[z] / ( float )_pawn.MaxHP( z );
        Child<Image>( children, 1 ).color = c;
    }

    return wboxItem;

    float doText( string contents, float textY ) {
        var wboxText = wbox.TopLeft( wbox.W, wbox.H, y: textY );
        int handle = WBUI.MeasuredText( contents, wboxText, out float measureW, out float measureH,
                                        font: GUIUnity.font, fontSize: -1, handle: childHandle );
        wboxText = wboxText.TopLeft( wboxText.W, measureH );
        WBUI.Text_wg( contents, wboxText, font: GUIUnity.font, fontSize: -1, handle: handle );
        childHandle++;
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
