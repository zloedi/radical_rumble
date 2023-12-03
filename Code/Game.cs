using System;
using System.Collections.Generic;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

partial class Game {


public Action<string> Log = s => Qonsole.Log( s );
public Action<string> Error = s => Qonsole.Error( s );

public Shadow shadow = new Shadow();

public Pawn pawn = new Pawn();
public Board board = new Board();

// rows sent over the network
public Array [] syncedRows;
// rows stored on map editor save
public Array [] persistentRows;
// resizeable board rows 
public Array [] gridRows;

public Game() {
    syncedRows = new Array [] {
        pawn.def,
        pawn.hp,
        pawn.pos0_tx,
        pawn.pos1_tx,

        board.size,
        board.terrain,
        board.flags,
    };

    persistentRows = new Array [] {
        board.size,
        board.terrain,
        board.flags,
    };

    gridRows = new Array [] {
        board.terrain,
        board.flags,
    };
}

public bool Init( bool skipShadowClones = false ) {
    shadow.Log = Log;
    shadow.Error = Error;
    if ( ! shadow.CreateShadows( pawn, skipClone: skipShadowClones,
                                                                ignoreType: typeof( Vector2 ) ) ) {
        return false;
    }
    if ( ! shadow.CreateShadows( board, skipClone: skipShadowClones ) ) {
        return false;
    }
    return true;
}

public void Reset() {
    pawn.Reset();
}

List<ushort> deltaChange = new List<ushort>();
List<int> deltaNumbers = new List<int>();
public bool UndeltaState( string [] argv, out bool updateBoardFilters ) {
    updateBoardFilters = false;

    if ( argv.Length < 1 ) {
        return false;
    }

    bool result = false;

    for ( int idx = 0; idx < argv.Length; ) {
        string rowName = argv[idx++];

        if ( ! shadow.nameToArray.TryGetValue( rowName, out Array row ) ) {
            Error( $"Undelta: Can't find {rowName} in row names." );
            continue;
        }

        if ( ! shadow.arrayToShadow.TryGetValue( row, out Shadow.Row shadowRow ) ) {
            Error( $"Undelta: Can't find {rowName} in shadows." );
            continue;
        }

        if ( Delta.UndeltaNum( ref idx, argv, deltaChange, deltaNumbers, out bool keepGoing ) ) {
            result = true;
            if ( shadowRow.parentObject == board ) {
                updateBoardFilters = true;
            }
            if ( shadowRow.type == Shadow.DeltaType.Uint8 ) {
                for ( int i = 0; i < deltaChange.Count; i++ ) {
                    ( ( byte [] )row )[deltaChange[i]] = ( byte )deltaNumbers[i];
                }
            } else if ( shadowRow.type == Shadow.DeltaType.Uint16 ) {
                for ( int i = 0; i < deltaChange.Count; i++ ) {
                    ( ( ushort [] )row )[deltaChange[i]] = ( ushort )deltaNumbers[i];
                }
            } else if ( shadowRow.type == Shadow.DeltaType.Int32 ) {
                for ( int i = 0; i < deltaChange.Count; i++ ) {
                    ( ( int [] )row )[deltaChange[i]] = ( int )deltaNumbers[i];
                }
                if ( row == pawn.pos0_tx ) {
                    for ( int i = 0; i < deltaChange.Count; i++ ) {
                        pawn.pos0[deltaChange[i]] = TxToV( deltaNumbers[i] );
                    }
                } else if ( row == pawn.pos1_tx ) {
                    for ( int i = 0; i < deltaChange.Count; i++ ) {
                        pawn.pos1[deltaChange[i]] = TxToV( deltaNumbers[i] );
                    }
                }
            }
        }

        if ( ! keepGoing ) {
            break;
        }
    }

#if false
    if ( persist && argv.Length > 1 && board.numItems > 0 ) {
#if UNITY_STANDALONE
        List<ushort> list = new List<ushort>();
        for ( int i = 0; i < board.numItems; i++ ) {
            if ( board.terrain[i] != 0 ) {
                list.Add( ( ushort )i );
            }
        }
        Hexes.PrintList( list, board.width, board.height, logText: "Undelta Board grid",
                                        hexListString: (l,i) => $"{l[i].x},{l[i].y}", hexSize: 48 );
#endif
    }
#endif

    return result;
}

const int FRAC_BITS = 8;
public static int ToTx( float f ) {
    int dec = ( int )f;
    int frac = ( int )( ( f - dec ) * ( 1 << FRAC_BITS ) );
    return dec << FRAC_BITS | frac;
}

public static int ToTx( Vector2 v ) {
    return ( ToTx( v.x ) << 16 ) | ToTx( v.y );
}

public static float TxToF( int tx ) {
    const float fracDenom = 1 << FRAC_BITS;

    tx &= 0xffff;
    float dec = tx >> FRAC_BITS;
    float frac = ( tx & ( ( 1 << FRAC_BITS ) - 1 ) ) / fracDenom;
    return dec + frac;
}

public static Vector2 TxToV( int tx ) {
    return new Vector2( TxToF( tx >> 16 ), TxToF( tx ) );
}

public static bool CanReach( int hxA, int hxB ) {
    return false;
}


}
