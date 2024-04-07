using System;
using System.Collections.Generic;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

namespace RR { partial class Game {


public Action<string> Log = s => Qonsole.Log( s );
public Action<string> Error = s => Qonsole.Error( s );

public ArrayShadow shadow = new ArrayShadow();

public Player player = new Player();
public Pawn pawn = new Pawn();
public Board board = new Board();

// pawns registered into the board for faster queries
public Dictionary<ushort,List<byte>> gridPawn = new Dictionary<ushort,List<byte>>();

// rows sent over the network
public Array [] syncedRows;
// rows stored on map editor save
public Array [] persistentRows;
// resizeable board rows 
public Array [] gridRows;

public Game() {
    syncedRows = new Array [] {
        player.zport,
        player.team,
        player.manaFull_ms,

        pawn.def,
        pawn.hp,
        pawn.team,
        pawn.focus,
        pawn.mvEnd_ms,
        pawn.mvEnd_tx,
        pawn.atkEnd_ms,

        board.size,
        board.terrain,
        board.zone,
    };

    persistentRows = new Array [] {
        board.size,
        board.terrain,
        board.zone,
        board.pawnDef,
        board.pawnTeam,
    };

    gridRows = new Array [] {
        board.terrain,
        board.zone,
        board.pawnDef,
        board.pawnTeam,
    };
}

public bool Init( bool skipShadowClones = false ) {
    shadow.Log = Log;
    shadow.Error = Error;

    if ( ! shadow.CreateShadows( player, maxRow: 0, skipClone: skipShadowClones,
                                                                            // ignored types
                                                                            typeof( float ) ) ) {
        return false;
    }
    if ( ! shadow.CreateShadows( pawn, maxRow: 0, skipClone: skipShadowClones,
                                                            // ignored types
                                                            typeof( float ), typeof( Vector2 ) ) ) {
        return false;
    }
    if ( ! shadow.CreateShadows( board, skipClone: skipShadowClones ) ) {
        return false;
    }

    return true;
}

public void Reset() {
    Log( "[ffc000]RESETTING THE GAME STATE[-]" );
    player.Reset();
    pawn.Reset();
    board.Reset();
    _pathCache.Clear();
    gridPawn.Clear();
}

// 0 -- keep running, 1 -- team0 win, 2 -- team1 win, 3 -- draw
public int IsOver() {
    for ( int team = 0; team < 2; team++ ) {
        int otherTeam = ( team + 1 ) & 1;
        if ( pawn.filter.objectives[team].Count == 0 ) {
            if ( pawn.filter.objectives[otherTeam].Count == 0 ) {
                return 3;
            }
            return 1 + team;
        }
    }
    return 0;
}

List<ushort> deltaChange = new List<ushort>();
List<int> deltaNumbers = new List<int>();
public bool UndeltaState( string [] argv, int clock, out bool updateBoard,
                                                            Pawn.ClientTrigger [] pawnTrig = null ) {
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

        if ( ! shadow.arrayToShadow.TryGetValue( row, out ArrayShadow.Row shadowRow ) ) {
            Error( $"Undelta: Can't find {rowName} in shadows." );
            continue;
        }

        if ( Delta.UndeltaNum( ref idx, argv, deltaChange, deltaNumbers, out bool keepGoing ) ) {
            result = true;
            if ( shadowRow.parentObject == board ) {
                updateBoard = true;
            }
            if ( shadowRow.type == ArrayShadow.DeltaType.Uint8 ) {
                for ( int i = 0; i < deltaChange.Count; i++ ) {
                    ( ( byte [] )row )[deltaChange[i]] = ( byte )deltaNumbers[i];
                }

                if ( row == pawn.def ) {

                    // clear garbage
                    for ( int i = 0; i < deltaChange.Count; i++ ) {
                        if ( deltaNumbers[i] == 0 ) {
                            pawn.Clear( deltaChange[i] );
                        }
                    }

                    // newly spawned
                    if ( pawnTrig != null ) {
                        for ( int i = 0; i < deltaChange.Count; i++ ) {
                            if ( deltaNumbers[i] != 0 ) {
                                pawnTrig[deltaChange[i]] |= Pawn.ClientTrigger.Spawn;
                            }
                        }
                    }
                }

            } else if ( shadowRow.type == ArrayShadow.DeltaType.Uint16 ) {
                for ( int i = 0; i < deltaChange.Count; i++ ) {
                    ( ( ushort [] )row )[deltaChange[i]] = ( ushort )deltaNumbers[i];
                }
            } else if ( shadowRow.type == ArrayShadow.DeltaType.Int32 ) {
                for ( int i = 0; i < deltaChange.Count; i++ ) {
                    ( ( int [] )row )[deltaChange[i]] = ( int )deltaNumbers[i];
                }
                
                // the move-end points are transferred as fixed point
                // take care of them implicitly here
                if ( row == pawn.mvEnd_tx ) {
                    for ( int i = 0; i < deltaChange.Count; i++ ) {
                        pawn.mvEnd[deltaChange[i]] = TxToV( deltaNumbers[i] );
                    }
                    
                    // raise the movement triggers on the client
                    if ( pawnTrig != null ) {
                        for ( int i = 0; i < deltaChange.Count; i++ ) {
                            pawnTrig[deltaChange[i]] |= Pawn.ClientTrigger.Move;
                        }
                    }
                }

                if ( row == pawn.atkEnd_ms && pawnTrig != null ) {
                    for ( int i = 0; i < deltaChange.Count; i++ ) {
                        pawnTrig[deltaChange[i]] |= Pawn.ClientTrigger.Attack;
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
        Log( "Update board filters." );
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

public bool BoardHasDef( int hx, Pawn.Def def ) {
    return Pawn.defs[board.pawnDef[hx]] == def;
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

public static Vector2Int VToAxial( Vector2 v ) {
    return Hexes.ScreenToHex( v );
}

public static Vector2 AxialToV( Vector2Int axial ) {
    return Hexes.HexToScreen( axial );
}

public Vector2 HexToV( int hx ) {
    return AxialToV( board.Axial( hx ) );
}

// https://dominoc925.blogspot.com/2012/02/c-code-snippet-to-determine-if-point-is.html
public static bool IsPointInPolygon( List<Vector2> polygon, Vector2 point ) {
    bool isInside = false;
    for ( int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++ ) {
        if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) && (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)) {
            isInside = !isInside;
        }
    }
    return isInside;
}


} }
