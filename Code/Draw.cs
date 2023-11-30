using System;
using System.Collections.Generic;
using UnityEngine;

using Cl = RRClient;

static class Draw {


public static int pixelSize => Mathf.Max( 1, Mathf.Min( Screen.height, Screen.width ) / 300 );
public static int hexPixelSize => 12 * pixelSize;

public static WrapBox wboxScreen;
public static string centralBigRedMessage;

public static Board board => Cl.game.board;
public static Pawn pawn => Cl.game.pawn;

public static readonly Color bgrColor = new Color( 0.2f, 0.2f, 0.25f );

static Vector2Int _pan;

public static void FillScreen( Color? color = null ) {
    Color c = color == null ? bgrColor : color.Value;
    Draw.FillRect( Draw.wboxScreen, c );
}

public static void BigRedMessage() {
    if ( ! string.IsNullOrEmpty( centralBigRedMessage ) ) {
        OutlinedTextCenter( Screen.width / 2, Screen.height / 2, Draw.centralBigRedMessage,
                                                                      color: Color.red, scale: 2 );
    }
}

public static void FillRect( WrapBox wbox, Color color ) {
    QGL.LateBlit( null, wbox.x, wbox.y, wbox.w, wbox.h, color: color );
}

public static void OutlinedTextCenter( int x, int y, string text, Color? color = null,
                                                                                int scale = 1 ) {
    scale *= pixelSize;
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

public static Vector2 ScreenToGamePosition( Vector2 xy ) {
    Vector2 origin = Hexes.HexToScreen( Vector2Int.zero, hexPixelSize );
    xy -= _pan;
    xy /= hexPixelSize;
    return xy - origin;
}

public static Vector2 HexToScreen( int x, int y ) {
    return Hexes.HexToScreen( x, y, hexPixelSize );
}

public static Vector2Int ScreenToHex( Vector2 xy ) {
    return Hexes.ScreenToHex( xy - _pan, hexPixelSize );
}

public static class model {
    public static Vector2 [] pos = new Vector2[Pawn.MAX_PAWN];
    public static float [] t = new float[Pawn.MAX_PAWN];
}

public static void PawnSprites( bool skipModels = false ) {
    Vector2 dsprite = new Vector2( Hexes.hexSpriteRegularWidth, Hexes.hexSpriteRegularHeight );
    Vector2 size = dsprite * Draw.pixelSize;
    Vector2 offPrn = new Vector2( Draw.pixelSize, Draw.pixelSize * 3 / 2 );
    Vector2 offShadow = new Vector2( Draw.pixelSize, Draw.pixelSize );

    var offShad = offShadow;
    var sz = size * 0.75f;
    var szHalf = sz * 0.5f;

    Vector2 [] posRow = skipModels ? pawn.pos0 : model.pos;

    void getScreenPos( int z, out Vector2 pos, out Vector2 topLeft ) {
        pos = _pan + posRow[z] * Draw.hexPixelSize;
        topLeft = pos - szHalf;
    }

    foreach ( var z in pawn.filter.no_flying ) {
        getScreenPos( z, out Vector2 scrPos, out Vector2 pos );
        QGL.LateBlit( Hexes.hexSpriteRegular, pos, sz, color: Color.black * 0.3f );
        Pawn.Def def = Pawn.defs[pawn.def[z]];
        Color c = new Color( def.color.r * 0.5f, def.color.g * 0.5f, def.color.b * 0.5f );
        QGL.LateBlit( Hexes.hexSpriteRegular, pos - offShad, sz, color: c );
        //Hexes.DrawHexWithLines( scrPos - offShad, sz.x * 1.2f, def.color * 0.75f );
        QGL.LatePrint( def.name[0], scrPos + offPrn - offShad, color: def.color, scale: Draw.pixelSize );
    }

    offShad = offShadow * 7;
    sz = size * 1;
    szHalf = sz * 0.5f;

    foreach ( var z in pawn.filter.flying ) {
        getScreenPos( z, out Vector2 scrPos, out Vector2 pos );
        QGL.LateBlit( Hexes.hexSpriteRegular, pos, sz, color: Color.black * 0.3f );
    }

    foreach ( var z in pawn.filter.flying ) {
        getScreenPos( z, out Vector2 scrPos, out Vector2 pos );
        Pawn.Def def = pawn.GetDef( z );
        Color c = new Color( def.color.r * 0.5f, def.color.g * 0.5f, def.color.b * 0.5f );
        QGL.LateBlit( Hexes.hexSpriteRegular, pos - offShad, sz, color: c );
        QGL.LatePrint( def.name[0], scrPos + offPrn - offShad, color: def.color, scale: Draw.pixelSize );
    }
}

public static void Board( Color? colorSolid = null, bool skipVoidHexes = false ) {
    Vector2 hexToScreen( ushort hx ) {
        return _pan + Hexes.HexToScreen( board.Axial( hx ), hexPixelSize );
    }

    void drawHex( ushort hx, Color c ) {
        Vector2 scr = hexToScreen( hx );
        int w = Hexes.hexSpriteRegularWidth * Draw.pixelSize;
        int h = Hexes.hexSpriteRegularHeight * Draw.pixelSize;
        QGL.LateBlit( Hexes.hexSpriteRegular, ( int )( scr.x - w / 2 ), ( int )( scr.y - h / 2 ),
                                                                                w, h, color: c );
    }

    Color csolid = colorSolid != null ? colorSolid.Value : new Color( 0.54f, 0.5f, 0.4f );

    // draw void hexes in grid range
    Color cvoid = Draw.bgrColor;
    cvoid *= 0.75f;
    cvoid.a = 1;

    if ( ! skipVoidHexes ) {
        foreach ( ushort hx in board.filter.no_solid ) {
            drawHex( hx, cvoid );
        }
    }

    foreach ( ushort hx in board.filter.solid ) {
        drawHex( hx, csolid );
    }

    foreach ( ushort hx in board.filter.solid ) {
        Vector2 scr = hexToScreen( hx );
        Hexes.DrawHexWithLines( scr, 11 * Draw.pixelSize, Color.black * 0.1f );
    }
}

public static void CenterBoardOnScreen() {
    GetBoardBoundsInPixels( out int x, out int y, out int w, out int h );
    _pan.x = -x + ( Screen.width - w ) / 2;
    _pan.y = -y + ( Screen.height - h ) / 2;
}

public static void OffsetView( Vector2 xy ) {
    GetBoardBoundsInPixels( out int x, out int y, out int w, out int h );
    _pan.x += ( int )xy.x;
    _pan.y += ( int )xy.y;
    int xmin = x;
    int xmax = x + w;
    int ymin = y;
    int ymax = y + h;
    _pan.x = Mathf.Min( _pan.x, Screen.width - xmin );
    _pan.y = Mathf.Min( _pan.y, Screen.height - ymin );
    _pan.x = Mathf.Max( _pan.x, 1 - xmax );
    _pan.y = Mathf.Max( _pan.y, 1 - ymax );
}

static void GetBoardBoundsInPixels( out int x, out int y, out int w, out int h ) {
    if ( board.filter.solid.Count == 0 ) {
        x = y = w = h = 0;
        return;
    }

    int minx = 999999, miny = 999999;
    int maxx = 0, maxy = 0;
    foreach ( ushort hx in board.filter.solid ) {
        Vector2 p = Hexes.HexToScreen( board.Axial( hx ), hexPixelSize );
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


}
