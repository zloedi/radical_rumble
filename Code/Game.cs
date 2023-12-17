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

public Dictionary<ushort,List<byte>> gridPawn = new Dictionary<ushort,List<byte>>();

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
        pawn.team,
        pawn.mvEndTime,
        pawn.mvEnd_tx,

        board.size,
        board.terrain,
    };

    persistentRows = new Array [] {
        board.size,
        board.terrain,
        board.pawnDef,
        board.pawnTeam,
    };

    gridRows = new Array [] {
        board.terrain,
        board.pawnDef,
        board.pawnTeam,
    };
}

public bool Init( bool skipShadowClones = false ) {
    shadow.Log = Log;
    shadow.Error = Error;
    if ( ! shadow.CreateShadows( pawn, 0, skipShadowClones, typeof( float ), typeof( Vector2 ) ) ) {
        return false;
    }
    if ( ! shadow.CreateShadows( board, skipClone: skipShadowClones ) ) {
        return false;
    }
    return true;
}

public void Reset() {
    Log( "[ffc000]RESETTING THE GAME STATE[-]" );
    pawn.Reset();
    _pathCache.Clear();
    gridPawn.Clear();
}

List<ushort> deltaChange = new List<ushort>();
List<int> deltaNumbers = new List<int>();
public bool UndeltaState( string [] argv, int clock, out bool updateBoard ) {
    updateBoard = false;

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
                updateBoard = true;
            }
            if ( shadowRow.type == Shadow.DeltaType.Uint8 ) {
                for ( int i = 0; i < deltaChange.Count; i++ ) {
                    ( ( byte [] )row )[deltaChange[i]] = ( byte )deltaNumbers[i];
                }
                if ( row == pawn.def ) {
                    // clear garbage pawns on death
                    for ( int i = 0; i < deltaChange.Count; i++ ) {
                        if ( deltaNumbers[i] == 0 ) {
                            pawn.Clear( deltaChange[i] );
                        }
                    }
                }
            } else if ( shadowRow.type == Shadow.DeltaType.Uint16 ) {
                for ( int i = 0; i < deltaChange.Count; i++ ) {
                    ( ( ushort [] )row )[deltaChange[i]] = ( ushort )deltaNumbers[i];
                }
            } else if ( shadowRow.type == Shadow.DeltaType.Int32 ) {
                for ( int i = 0; i < deltaChange.Count; i++ ) {
                    ( ( int [] )row )[deltaChange[i]] = ( int )deltaNumbers[i];
                }
                
                if ( row == pawn.mvEnd_tx ) {
                    // new movement segment arrives, trigger movement on the client
                    for ( int i = 0; i < deltaChange.Count; i++ ) {
                        pawn.mvStart[deltaChange[i]] = pawn.mvPos[deltaChange[i]];
                    }
                    for ( int i = 0; i < deltaChange.Count; i++ ) {
                        pawn.mvEnd[deltaChange[i]] = TxToV( deltaNumbers[i] );
                    }
                    for ( int i = 0; i < deltaChange.Count; i++ ) {
                        pawn.mvStartTime[deltaChange[i]] = clock;
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

    if ( updateBoard ) {
        board.UpdateFilters();
        gridPawn.Clear();
        _pathCache.Clear();
    }

    return result;
}

// assumes filters are updated
public void RegisterIntoGrids() {
    foreach ( var l in gridPawn.Values ) {
        l.Clear();
    }
    foreach ( int z in pawn.filter.no_garbage ) {
        ushort hex = ( ushort )VToHex( pawn.mvPos[z] );
        List<byte> l;
        if ( ! gridPawn.TryGetValue( hex, out l ) ) {
            l = gridPawn[hex] = new List<byte>();
        }
        l.Add( ( byte )z );
    }
}

public bool GetFirstPawnOnHex( int hx, out int z ) {
    if ( GetPawnsOnHex( hx, out List<byte> l ) ) {
        z = l[0];
        return true;
    }
    z = 0;
    return false;
}

public bool GetPawnsOnHex( int hx, out List<byte> l ) {
    if ( ! gridPawn.TryGetValue( ( ushort )hx, out l ) ) {
        l = gridPawn[( ushort )hx] = new List<byte>();
    }
    return l.Count > 0;
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

public int VToHex( Vector2 v ) {
    return board.Hex( VToAxial( v ) );
}

public Vector2Int VToAxial( Vector2 v ) {
    return Hexes.ScreenToHex( v );
}

public Vector2 AxialToV( Vector2Int axial ) {
    return Hexes.HexToScreen( axial );
}

public Vector2 HexToV( int hx ) {
    return AxialToV( board.Axial( hx ) );
}

public bool BoardHasDef( int hx, Pawn.Def def ) {
    return Pawn.defs[board.pawnDef[hx]] == def;
}


}
