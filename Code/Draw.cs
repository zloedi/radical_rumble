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

public static Vector2Int AxialToScreenNoPan( int x, int y ) {
    Vector2 v = Hexes.HexToScreen( x, y, hexPixelSize );
    return new Vector2Int( ( int )v.x, ( int )v.y );
}

public static Vector2Int AxialToScreen( Vector2Int axial ) {
    return AxialToScreen( axial.x, axial.y );
}

public static Vector2Int AxialToScreen( int x, int y ) {
    return _pan + AxialToScreenNoPan( x, y );
}

public static Vector2 HexToScreen( int hx ) {
    return AxialToScreen( board.Axial( hx ) );
}

public static int ScreenToHex( Vector2 xy ) {
    Vector2Int axial = ScreenToAxial( xy );
    return board.Hex( axial );
}

public static Vector2Int ScreenToAxial( Vector2 xy ) {
    return Hexes.ScreenToHex( xy - _pan, hexPixelSize );
}

public static class model {
    public static Vector2 [] pos = new Vector2[Pawn.MAX_PAWN];
    public static float [] t = new float[Pawn.MAX_PAWN];

    public static void Clear() {
        Array.Clear( pos, 0, pos.Length );
        Array.Clear( t, 0, t.Length );
    }
}

public static void TerrainTile( int x, int y, Color? c = null, float sz = 1 ) {
    Vector2 scr = AxialToScreen( x, y );
    int w = ( int )( Hexes.hexSpriteRegularWidth * Draw.pixelSize * sz );
    int h = ( int )( Hexes.hexSpriteRegularHeight * Draw.pixelSize * sz );
    QGL.LateBlit( Hexes.hexSpriteRegular, ( int )( scr.x - w / 2 ), ( int )( scr.y - h / 2 ),
                                                                            w, h, color: c );
}

public static void TerrainTile( Vector2Int axial, Color? c = null, float sz = 1 ) {
    TerrainTile( axial.x, axial.y, c, sz );
}

public static void TerrainTile( int hx, Color? c = null, float sz = 1 ) {
    TerrainTile( board.Axial( hx ), c, sz );
}

public static void PawnSprites( bool skipModels = false, float alpha = 1 ) {
    Vector2Int dsprite = new Vector2Int( Hexes.hexSpriteRegularWidth, Hexes.hexSpriteRegularHeight );
    Vector2Int size = dsprite * Draw.pixelSize;
    Vector2Int offPrn = new Vector2Int( Draw.pixelSize, Draw.pixelSize * 2 );
    Vector2Int offShadow = new Vector2Int( Draw.pixelSize, Draw.pixelSize );

    var offShad = offShadow;
    var sz = new Vector2Int( ( int )( size.x * 0.8f ), ( int )( size.y * 0.8f ) );
    var szHalf = sz / 2;

    Vector2 [] posRow = skipModels ? pawn.pos0 : model.pos;

    void blit( Vector2Int vpos, Vector2Int vsz, Color color ) {
        color.a *= alpha;
        QGL.LateBlit( Hexes.hexSpriteRegular, vpos, vsz, color: color );
    }

    void print( int z, Vector2Int vpos ) {
        Pawn.Def def = Pawn.defs[pawn.def[z]];
        Vector2Int v = vpos + szHalf + offPrn;
        var c = def.color;
        c.a = alpha;
        QGL.LatePrint( def.name[0], v, color: c, scale: Draw.pixelSize );
    }

    void getScreenPos( int z, out Vector2Int topLeft ) {
        Vector2 pos = _pan + posRow[z] * Draw.hexPixelSize - szHalf;
        topLeft = new Vector2Int( ( int )pos.x, ( int )pos.y );
    }

    foreach ( var z in pawn.filter.no_flying ) {
        getScreenPos( z, out Vector2Int pos );
        blit( pos, sz, color: Color.black * 0.3f );
        Pawn.Def def = Pawn.defs[pawn.def[z]];
        Color c = new Color( def.color.r * 0.5f, def.color.g * 0.5f, def.color.b * 0.5f );
        blit( pos - offShad, sz, color: c );
        print( z, pos - offShad );
    }

    offShad = offShadow * 7;
    sz = size;
    szHalf = sz / 2;

    foreach ( var z in pawn.filter.flying ) {
        getScreenPos( z, out Vector2Int pos );
        blit( pos, sz, color: Color.black * 0.3f );
    }

    foreach ( var z in pawn.filter.flying ) {
        getScreenPos( z, out Vector2Int pos );
        Pawn.Def def = pawn.GetDef( z );
        Color c = new Color( def.color.r * 0.5f, def.color.g * 0.5f, def.color.b * 0.5f );
        blit( pos - offShad, sz, color: c );
        print( z, pos - offShad );
    }
}

public static void Board( Color? colorSolid = null, bool skipVoidHexes = false ) {
    void drawHex( ushort hx, Color c ) {
        Vector2 scr = HexToScreen( hx );
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
        Vector2 scr = HexToScreen( hx );
        Hexes.DrawHexWithLines( scr, 11 * Draw.pixelSize, Color.black * 0.1f );
    }
}

public static void BoardBounds() {
    GetBoardBoundsInPixels( out int x, out int y, out int w, out int h );
    QGL.LateDrawLineRect( x + _pan.x, y + _pan.y, w, h );
}

public static void CenterBoardOnScreen() {
    GetBoardBoundsInPixels( out int x, out int y, out int w, out int h );
    _pan.x = ( Screen.width - w ) / 2 - x;
    _pan.y = ( Screen.height - h ) / 2 - y;
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
