using System;
using System.Collections.Generic;
using UnityEngine;

static class Draw {


public static int pixelSize => Mathf.Max( 1, Mathf.Min( Screen.height, Screen.width ) / 400 );

public static WrapBox wboxScreen;
public static string centralBigRedMessage;

public static Vector2Int panning;

//public static string bottomMessage;
//public static string bottomError;

public static void FillScreen( Color? color = null ) {
    Color c = color == null ? new Color( 0.2f, 0.2f, 0.25f ) : color.Value;
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

public static Vector2Int ScreenPosToHexCoord( Vector2 scrPos ) {
    int w = Hexes.hexSpriteWidth * pixelSize;

    float a = w+pixelSize*2; float b = w/2+pixelSize;
    float c = 0             ; float d = w-pixelSize*2;

    float det = (a * d - b * c);

    float aa =  d / det; float bb = -b / det;
    float cc = -c / det; float dd =  a / det;

    Vector2 axial = new Vector2(
        ( scrPos.x - panning.x ) * aa + ( scrPos.y - panning.y ) * bb,
        ( scrPos.x - panning.x ) * cc + ( scrPos.y - panning.y ) * dd
    );

    return Hexes.AxialRound( axial );
}

public static Vector2Int HexCoordToSqGrid( int hxcx, int hxcy ) {
    int w = Hexes.hexSpriteWidth;
    int h = Hexes.hexSpriteHeight;

    int x = hxcx * ( w + 2 ) + hxcy * ( w / 2 + 1 );
    int y = hxcx * 0         + hxcy * ( w - 2 );

    return new Vector2Int( x, y );
}

public static Vector2Int HexCoordToScreen( int hxcx, int hxcy ) {
    return HexCoordToSqGrid( hxcx, hxcy ) * pixelSize;
}

public static Vector2Int HexCoordToScreen( Vector2Int hxc ) {
    return HexCoordToScreen( hxc.x, hxc.y );
}

public static Vector2Int HexCoordToScreenPan( Vector2Int hxc ) {
    return HexCoordToScreen( hxc.x, hxc.y ) + panning;
}

public static void HexScreen( Vector2Int scrPos, Color color ) {
    int w = Hexes.hexSpriteWidth * pixelSize;
    int h = Hexes.hexSpriteHeight * pixelSize;
    QGL.LateBlit( Hexes.hexSprite, scrPos.x - w / 2, scrPos.y - w / 2, w, h, color: color );
}

public static void Hex( Vector2Int hxc, Color color ) {
    HexScreen( HexCoordToScreenPan( hxc ), color );
}


}
