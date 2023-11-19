using System;
using System.Collections.Generic;

class Pawn {

public const int MAX_PAWN = 256;

static List<Array> _allRows = new List<Array>();

static T [] RegisterRow<T>() {
    var r = new T[MAX_PAWN];
    _allRows.Add( r );
    return r; 
}

public byte [] hp = RegisterRow<byte>();

public void Reset() {
    foreach ( var r in _allRows ) {
        Array.Clear( r, 0, r.Length );
    }
}


}
