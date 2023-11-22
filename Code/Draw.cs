using System;
using UnityEngine;

static class Draw {


public static int pixelSize => Mathf.Max( 1, Mathf.Min( Screen.height, Screen.width ) / 400 );

public static WrapBox wboxScreen;
public static string centralBigRedMessage;

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

    for ( int i = 0; i < offset.Length; i += 2 ) {
        QGL.LatePrintNokia( text, x + offset[i + 0] * scale, y + offset[i + 1] * scale,
                                                        color: Color.black, scale: scale );
    }
    QGL.LatePrintNokia( text, x, y, color: color, scale: scale );
}


}
