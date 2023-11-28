using System;
using System.Collections.Generic;
using UnityEngine;

static class Draw {


public static int pixelSize => Mathf.Max( 1, Mathf.Min( Screen.height, Screen.width ) / 300 );

public static WrapBox wboxScreen;
public static string centralBigRedMessage;

public static readonly Color bgrColor = new Color( 0.2f, 0.2f, 0.25f );
//public static string bottomMessage;
//public static string bottomError;

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

public static void Board( int boardW, List<ushort> solid, List<ushort> no_solid,
                                                                        Color? colorSolid = null ) {
    Vector2Int hexToCoord( int hx ) {
        int w = boardW == 0 ? 256 : boardW;
        return new Vector2Int( hx % w, hx / w );
    }

    void drawHex( ushort hx, Color c ) {
        Vector2Int axial = hexToCoord( hx );
        //Vector2 scr = Hexes.HexToScreen( axial.x, axial.y, 12 / Hexes.SQRT_3 * Draw.pixelSize );
        Vector2 scr = Hexes.HexToScreen( axial, 12 * Draw.pixelSize );
        int w = Hexes.hexSpriteWidth * Draw.pixelSize;
        int h = Hexes.hexSpriteHeight * Draw.pixelSize;
        QGL.LateBlit( Hexes.hexSpriteRegular, ( int )( scr.x - w / 2 ), ( int )( scr.y - h / 2 ),
                                                                                w, h, color: c );
    }

    Color csolid = colorSolid != null ? colorSolid.Value : new Color( 0.54f, 0.5f, 0.4f );

    // draw void hexes in grid range
    Color cvoid = Draw.bgrColor;
    cvoid *= 0.75f;
    cvoid.a = 1;

    foreach ( ushort hx in no_solid ) {
        drawHex( hx, cvoid );
    }

    foreach ( ushort hx in solid ) {
        drawHex( hx, csolid );
    }

    foreach ( ushort hx in solid ) {
        Vector2Int axial = hexToCoord( hx );
        Vector2 scr = Hexes.HexToScreen( axial, 12 * Draw.pixelSize );
        Hexes.DrawHexWithLines( scr, 11 * Draw.pixelSize, Color.black * 0.1f );
    }
}


}
