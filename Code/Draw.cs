using System;
using System.Collections.Generic;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
using SDLPorts;
#endif


namespace RR {


using Cl = Client;
using Trig = Pawn.ClientTrigger;


static class Draw {


// shadow offset for flyers
const int FLYERS_SHADOW_OFFSET = 5;

static bool SkipPawns_cvar = false;
static bool SkipBigRedMessage_cvar = false;

public static int pixelSize => Mathf.Max( 1, Mathf.Min( _screenWidth, _screenHeight ) / 300 );
public static int textSize => Mathf.Max( 1, pixelSize * 2 / 3 );
public static int hexPixelSize => 12 * pixelSize;

public static WrapBox wboxScreen;
public static string centralBigRedMessage;
public static int team;
public static float mana;
public static bool observer;
public static bool rotate180 => team != 0;

static readonly Color _bgrColor = new Color( 0.2f, 0.2f, 0.25f );

static int _screenWidth => ( int )wboxScreen.w;
static int _screenHeight => ( int )wboxScreen.h;
static Board _board => Cl.game.board;
static Pawn _pawn => Cl.game.pawn;
static Vector2Int _pan;
static Vector2Int _pawnSymbolOffset => new Vector2Int( pixelSize, pixelSize * 2 );

public static void WireCircleGame( Vector2 gamePos, float r, Color c ) {
    WireCircleScreen( GTS( gamePos ), r * hexPixelSize, c );
}

static Vector2 [] _circle = new Vector2[14];
public static void WireCircleScreen( Vector2 screenPos, float r, Color c ) {
    int max = _circle.Length;
    float step = ( float )( Math.PI * 2f / max );
    for ( int i = 0; i < max; i++ ) {
        Vector2 v = new Vector2( Mathf.Cos( i * step ), Mathf.Sin( i * step ) );
        _circle[i] = v * r + screenPos;
    }
    QGL.LateDrawLineLoop( _circle, color: c );
}

public static void SegmentGame( Vector2 a, Vector2 b, Color c ) {
    QGL.LateDrawLine( GTS( a ), GTS( b ), color: c );
}

public static void FillScreen( Color? color = null ) {
    Color c = color == null ? _bgrColor : color.Value;
    FillRect( wboxScreen, c );
}

public static void BigRedMessage() {
    if ( ! SkipBigRedMessage_cvar && ! string.IsNullOrEmpty( centralBigRedMessage ) ) {
        OutlinedTextCenter( _screenWidth / 2, _screenHeight / 2, centralBigRedMessage,
                                                                      color: Color.red, scale: 2 );
    }
}

public static void FillRect( WrapBox wbox, Color color ) {
    QGL.LateBlit( wbox.x, wbox.y, wbox.w, wbox.h, color: color );
}

public static void OutlinedTextCenter( int x, int y, string text, Color? color = null,
                                                                                float scale = 1 ) {
    scale = ( int )( scale * pixelSize + 0.5f );
    color = color != null ? color : Color.white;
    
    int [] offset = {
        0, -1,
        -1, -1,
        -1, 0,
        0, 1,
        1, 1,
        1, 0,
        -1, 1,
        1, -1,
    };

    var black = Color.black;
    black.a = color.Value.a * color.Value.a * color.Value.a;
    for ( int i = 0; i < offset.Length; i += 2 ) {
        QGL.LatePrintNokia( text, x + offset[i + 0] * scale, y + offset[i + 1] * scale,
                                                                    color: black, scale: scale );
    }
    QGL.LatePrintNokia( text, x, y, color: color, scale: scale );
}

static List<Vector2> _zoneBuf = new List<Vector2>();

static void GetZoneBuf( Board.Zone zn ) {
    _zoneBuf.Clear();
    foreach ( var hx in zn.polygon ) {
        Board.ZoneData zd = _board.UnpackZoneData( _board.zone[hx] );
        _zoneBuf.Add( HexToScreen( hx ) );
    }
}

public static bool IsPointInZone( Board.Zone zn, Vector2 screenPoint ) {
    GetZoneBuf( zn );
    return Game.IsPointInPolygon( _zoneBuf, screenPoint );
}

public static Vector2 STG( Vector2 xy ) {
    return ScreenToGamePosition( xy );
}

public static Vector2 ScreenToGamePosition( Vector2 xy ) {
    Vector2 origin = Hexes.HexToScreen( Vector2Int.zero, hexPixelSize );
    xy = InvertScreenTransform( xy );
    xy /= hexPixelSize;
    return xy - origin;
}

public static Vector2 GTS( Vector2 gamePos ) {
    return GameToScreenPosition( gamePos );
}

public static Vector2 GameToScreenPosition( Vector2 gamePos ) {
    return ApplyScreenTransform( gamePos * hexPixelSize );
}

public static Vector2Int AxialToScreenNoPan( int x, int y ) {
    Vector2 v = Hexes.HexToScreen( x, y, hexPixelSize );
    return new Vector2Int( ( int )v.x, ( int )v.y );
}

public static Vector2Int AxialToScreen( Vector2Int axial ) {
    return AxialToScreen( axial.x, axial.y );
}

public static Vector2Int AxialToScreen( int x, int y ) {
    return ApplyScreenTransform( AxialToScreenNoPan( x, y ) );
}

public static Vector2Int HexToScreen( int hx ) {
    return AxialToScreen( _board.Axial( hx ) );
}

public static int ScreenToHex( Vector2 xy ) {
    Vector2Int axial = ScreenToAxial( xy );
    return _board.Hex( axial );
}

public static Vector2Int ScreenToAxial( Vector2 xy ) {
    xy = InvertScreenTransform( xy );
    return Hexes.ScreenToHex( xy, hexPixelSize );
}

public static void TerrainTile( int x, int y, Color? c = null, float sz = 1 ) {
    Vector2 scr = AxialToScreen( x, y );
    int w = ( int )( Hexes.hexSpriteRegularWidth * pixelSize * sz );
    int h = ( int )( Hexes.hexSpriteRegularHeight * pixelSize * sz );
    QGL.LateBlit( Hexes.hexSpriteRegular, ( int )( scr.x - w / 2 ), ( int )( scr.y - h / 2 ),
                                                                            w, h, color: c );
}

public static void TerrainTile( Vector2Int axial, Color? c = null, float sz = 1 ) {
    TerrainTile( axial.x, axial.y, c, sz );
}

public static void TerrainTile( int hx, Color? c = null, float sz = 1 ) {
    TerrainTile( _board.Axial( hx ), c, sz );
}

public static void Zones( bool allTeams = false ) {
    foreach ( var zn in _board.filter.zones ) {
        if ( zn.polygon.Count == 0 ) {
            continue;
        }
        if ( ! allTeams && zn.team != team ) {
            continue;
        }
        Color col = zn.team == team ? Color.cyan : Color.red;
        GetZoneBuf( zn );
        QGL.LateDrawLineLoop( _zoneBuf, color: col );
    }
}

public static void PawnDef( Vector2 pos, int defIdx, float alpha, bool countDown ) {
    PawnDef( new Vector2Int( ( int )pos.x, ( int )pos.y ), defIdx, alpha, countDown );
}

public static void PawnDef( Vector2 pos, Pawn.Def def, float alpha, bool countDown ) {
    PawnDef( new Vector2Int( ( int )pos.x, ( int )pos.y ), def, alpha, countDown );
}

public static void PawnDef( Vector2Int pos, int defIdx, float alpha, bool countDown ) {
    PawnDef( pos, Pawn.defs[defIdx], alpha, countDown );
}

static int _floatAnimShadow => ( int )( pixelSize * Mathf.Sin( Time.time * 2 ) - pixelSize );
static int _floatAnim => ( int )( pixelSize * 1.5f * Mathf.Sin( Time.time * 2 ) );
static Color _colShadow => Color.black * 0.3f;
public static void PawnDef( Vector2Int pos, Pawn.Def def, float alpha, bool countDown ) {

    float d = def.radius * 2;
    Vector2Int dsprite = new Vector2Int( Hexes.hexSpriteRegularWidth,
                                                                    Hexes.hexSpriteRegularHeight );
    Vector2Int size = dsprite * pixelSize;
    Vector2Int sz = new Vector2Int( ( int )( size.x * d ), ( int )( size.y * d ) );
    Vector2Int szHalf = sz / 2;

    bool fly = ( def.flags & Pawn.Def.Flags.Flying ) != 0;

    int shadowBump = fly ? FLYERS_SHADOW_OFFSET : 1;
    Vector2Int offShadow = Vector2Int.one * pixelSize * shadowBump;

    // shadow
    Vector2Int szBob = sz + ( fly ? Vector2Int.one * _floatAnimShadow : Vector2Int.zero );
    QGL.LateBlit( Hexes.hexSpriteRegular, pos - szHalf, szBob, color: _colShadow * alpha );

    if ( countDown ) {
        Color manaCol = new Color( 0.9f, 0.2f, 0.9f ) * 2;
        int cd = ( int )( ( def.cost - mana ) * 10 + 1 );
        OutlinedTextCenter( pos.x, pos.y + ( int )( szHalf.y * 1.5f ),
                        cd.ToString(), color: Color.white, scale: 0.5f );
    }

    pos -= offShadow;
    pos.y += fly ? _floatAnim : 0;

    // sprite
    Color c = def.color * 0.5f;
    c.a = alpha;
    QGL.LateBlit( Hexes.hexSpriteRegular, pos - szHalf, sz, color: c );

    // symbol
    c = def.color;
    c.a = alpha;
    Vector2Int vv = pos + _pawnSymbolOffset;
    if ( def.symbol >= 'a' && def.symbol <= 'z' ) {
        vv.y -= pixelSize;
    }
    QGL.LatePrint( def.symbol, vv, color: c, scale: pixelSize );
}

static float [] _hurtBlink = new float[Pawn.MAX_PAWN];
public static void PawnSprites( float alpha = 1 ) {
    if ( SkipPawns_cvar ) {
        return;
    }

    Vector2Int dsprite = new Vector2Int( Hexes.hexSpriteRegularWidth, Hexes.hexSpriteRegularHeight );
    Vector2Int size = dsprite * pixelSize;
    Vector2Int offSymbol = _pawnSymbolOffset;

    Vector2Int offShad;
    int bobAnim;

    Vector2Int sz( int z ) {
        float d = _pawn.Radius( z ) + 0.3f;
        return new Vector2Int( ( int )( size.x * d ), ( int )( size.y * d ) );
    }

    Vector2Int szHalf( int z ) {
        return sz( z ) / 2;
    }

    void setParams( int shadowBump = 1, int bob = 0 ) {
        Vector2Int offShadow = Vector2Int.one * pixelSize;
        offShad = offShadow * shadowBump;
        bobAnim = bob;
    }

    void blit( int z, Vector2Int vpos, Color color ) {
        if ( _pawn.hp[z] == 0 ) {
            return;
        }
        if ( Cl.TrigIsOn( z, Trig.HurtVisuals ) ) {
            _hurtBlink[z] = 1.15f;
            SingleShot.AddConditional( dt => {
                _hurtBlink[z] -= dt * 5;
                _hurtBlink[z] = Mathf.Max( 0, _hurtBlink[z] );
                return _hurtBlink[z] > 0;
            } );
        }
        color.a *= alpha;

        color.r += _hurtBlink[z];
        color.g += _hurtBlink[z];
        color.b += _hurtBlink[z];

        Vector2Int szBob = sz( z ) + Vector2Int.one * bobAnim;
        QGL.LateBlit( Hexes.hexSpriteRegular, vpos, szBob, color: color );
    }

    void healthbar( int z, Vector2Int vpos ) {
        if ( _pawn.hp[z] == 0 ) {
            return;
        }
        Vector2Int vsz = sz( z );
        vpos.x += vsz.x / 2;
        vsz = new Vector2Int( vsz.x * 4 / 5, 4 * pixelSize );
        vpos.x -= vsz.x / 2;
        vpos.y -= vsz.y + pixelSize;
        QGL.LateBlit( null, vpos, vsz, color: Color.black * 0.5f );
        //QGL.LatePrint( _pawn.hp[z], vpos + new Vector2( vsz.x / 2, vsz.y * 2 ) );
        vsz.x -= pixelSize * 2;
        vsz.y -= pixelSize * 2;
        vpos += Vector2Int.one * pixelSize;
        Color c = _pawn.team[z] == team ? new Color( 0, 0.45f, 1f ) : Color.red;
        float t = _pawn.hp[z] / ( float )_pawn.MaxHP( z );
        vsz.x = Mathf.Max( 1, ( int )( vsz.x * t + 0.5f ) );
        QGL.LateBlit( null, vpos, vsz, color: c );
    }

    void symbol( int z, Vector2Int vpos ) {
        if ( _pawn.hp[z] == 0 ) {
            return;
        }
        Pawn.Def def = Pawn.defs[_pawn.def[z]];
        Vector2Int v = vpos + szHalf( z ) + offSymbol;
        if ( def.symbol >= 'a' && def.symbol <= 'z' ) {
            v.y -= pixelSize;
        }
        var c = def.color;
        c.a = alpha;
        c.r += _hurtBlink[z];
        c.g += _hurtBlink[z];
        c.b += _hurtBlink[z];
        QGL.LatePrint( def.symbol, v, color: c, scale: pixelSize );
    }

    void getScreenPos( int z, out Vector2Int topLeft ) {
        Vector2 pos = _pawn.mvPos[z] * hexPixelSize;
        topLeft = new Vector2Int( ( int )pos.x, ( int )pos.y );
        topLeft = ApplyScreenTransform( topLeft );
        topLeft -= szHalf( z );
    }

    // structures
    setParams();
    foreach ( var z in _pawn.filter.structures ) {
        getScreenPos( z, out Vector2Int pos );
        blit( z, pos, color: _colShadow );
        Pawn.Def def = Pawn.defs[_pawn.def[z]];
        Color c = new Color( def.color.r * 0.5f, def.color.g * 0.5f, def.color.b * 0.5f );
        blit( z, pos - offShad, color: c );
        symbol( z, pos - offShad );
    }

    // non-structure ground units
    setParams();
    foreach ( var z in _pawn.filter.no_structures ) {
        getScreenPos( z, out Vector2Int pos );
        blit( z, pos, color: _colShadow );
        Pawn.Def def = Pawn.defs[_pawn.def[z]];
        Color c = new Color( def.color.r * 0.5f, def.color.g * 0.5f, def.color.b * 0.5f );
        blit( z, pos - offShad, color: c );
        symbol( z, pos - offShad );
    }

    // flyers on top of other units
    setParams( shadowBump: FLYERS_SHADOW_OFFSET, bob: _floatAnimShadow );
    foreach ( var z in _pawn.filter.flying ) {
        getScreenPos( z, out Vector2Int pos );
        blit( z, pos, color: _colShadow );
    }

    setParams( shadowBump: FLYERS_SHADOW_OFFSET );
    foreach ( var z in _pawn.filter.flying ) {
        getScreenPos( z, out Vector2Int pos );
        pos.y += _floatAnim;
        Pawn.Def def = _pawn.GetDef( z );
        Color c = new Color( def.color.r * 0.5f, def.color.g * 0.5f, def.color.b * 0.5f );
        blit( z, pos - offShad, color: c );
        symbol( z, pos - offShad );
    }

    // == healthbars ==

    setParams();
    foreach ( var z in _pawn.filter.structures ) {
        getScreenPos( z, out Vector2Int pos );
        healthbar( z, pos - offShad );
    }

    setParams();
    foreach ( var z in _pawn.filter.no_structures ) {
        getScreenPos( z, out Vector2Int pos );
        healthbar( z, pos - offShad );
    }

    setParams( shadowBump: FLYERS_SHADOW_OFFSET );
    foreach ( var z in _pawn.filter.flying ) {
        getScreenPos( z, out Vector2Int pos );
        healthbar( z, pos - offShad );
    }
}

public static void Board( Color? colorSolid = null, bool skipVoidHexes = false ) {
    void drawHex( ushort hx, Color c ) {
        Vector2 scr = HexToScreen( hx );
        int w = Hexes.hexSpriteRegularWidth * pixelSize;
        int h = Hexes.hexSpriteRegularHeight * pixelSize;
        QGL.LateBlit( Hexes.hexSpriteRegular, ( int )( scr.x - w / 2 ), ( int )( scr.y - h / 2 ),
                                                                                w, h, color: c );
    }

    Color csolid = colorSolid != null ? colorSolid.Value : new Color( 0.54f, 0.5f, 0.4f );

    // draw void hexes in grid range
    Color cvoid = _bgrColor;
    cvoid *= 0.75f;
    cvoid.a = 1;

    if ( ! skipVoidHexes ) {
        foreach ( ushort hx in _board.filter.no_solid ) {
            drawHex( hx, cvoid );
        }
    }

    foreach ( ushort hx in _board.filter.solid ) {
        drawHex( hx, csolid );
    }

    foreach ( ushort hx in _board.filter.solid ) {
        Vector2 scr = HexToScreen( hx );
        Hexes.DrawHexWithLines( scr, 11 * pixelSize, Color.black * 0.1f );
    }
}

public static void BoardBounds() {
    GetBoardBoundsInPixels( out int x, out int y, out int w, out int h );
    QGL.LateDrawLineRect( x + _pan.x, y + _pan.y, w, h );
}

public static void CenterBoardOnScreen() {
    GetBoardBoundsInPixels( out int x, out int y, out int w, out int h );
    int gap = 20 * pixelSize;
    if ( observer ) {
        _pan.x = ( _screenWidth - w ) / 2 - x;
    } else {
        if ( rotate180 ) {
            _pan.x = ( _screenWidth - w ) / 2 - x + gap;
        } else {
            _pan.x = ( _screenWidth - w ) / 2 - x - gap;
        }
    }
    _pan.y = ( _screenHeight - h ) / 2 - y;
}

public static void OffsetView( Vector2 xy ) {
    GetBoardBoundsInPixels( out int x, out int y, out int w, out int h );
    _pan.x += ( int )xy.x;
    _pan.y += ( int )xy.y;
    int xmin = x;
    int xmax = x + w;
    int ymin = y;
    int ymax = y + h;
    _pan.x = Mathf.Min( _pan.x, _screenWidth - xmin );
    _pan.y = Mathf.Min( _pan.y, _screenHeight - ymin );
    _pan.x = Mathf.Max( _pan.x, 1 - xmax );
    _pan.y = Mathf.Max( _pan.y, 1 - ymax );
}

static void GetBoardBoundsInPixels( out int x, out int y, out int w, out int h ) {
    if ( _board.filter.solid.Count == 0 ) {
        x = y = w = h = 0;
        return;
    }

    int minx = 999999, miny = 999999;
    int maxx = 0, maxy = 0;
    foreach ( ushort hx in _board.filter.solid ) {
        Vector2 p = Hexes.HexToScreen( _board.Axial( hx ), hexPixelSize );
        minx = ( int )Mathf.Min( p.x, minx );
        miny = ( int )Mathf.Min( p.y, miny );
        maxx = ( int )Mathf.Max( p.x, maxx );
        maxy = ( int )Mathf.Max( p.y, maxy );
    }
    x = minx;
    y = miny;
    w = maxx - minx + 1;
    h = maxy - miny + 1;

    x -= Hexes.hexSpriteRegularWidth / 2 * pixelSize;
    w += Hexes.hexSpriteRegularWidth * pixelSize;

    y -= Hexes.hexSpriteRegularHeight / 2 * pixelSize;
    h += Hexes.hexSpriteRegularHeight * pixelSize;
}

// take into account 180 degrees rotation + panning
static Vector2 ApplyScreenTransform( Vector2 pos ) {
    pos += new Vector2( wboxScreen.x, wboxScreen.y );
    pos += _pan;
    if ( rotate180 ) {
        pos.x = _screenWidth - pos.x;
        pos.y = _screenHeight - pos.y;
    }
    return pos;
}

static Vector2Int ApplyScreenTransform( Vector2Int pos ) {
    pos += new Vector2Int( ( int )wboxScreen.x, ( int )wboxScreen.y );
    pos += _pan;
    if ( rotate180 ) {
        pos.x = _screenWidth - pos.x;
        pos.y = _screenHeight - pos.y;
    }
    return pos;
}

static Vector2 InvertScreenTransform( Vector2 pos ) {
    if ( rotate180 ) {
        pos.x = _screenWidth - pos.x;
        pos.y = _screenHeight - pos.y;
    }
    pos -= _pan;
    pos -= new Vector2Int( ( int )wboxScreen.x, ( int )wboxScreen.y );
    return pos;
}


} // Draw

} // RR
