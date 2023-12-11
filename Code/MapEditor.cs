using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;

using Cl = RRClient;

static class MapEditor {


static string LastSavedMap_cvar = "unnamed";

static int State_cvar = 1;
static string [] _tickNames;
static Action [] _ticks = TickUtil.RegisterTicks( typeof( MapEditor ), out _tickNames,
    None_tck,
    PlaceTerrain_tck,
    PlaceTowers_tck,
    PatherTest_tck,
    HexTracing_tck
);

static string _stateName => _tickNames[State_cvar % _ticks.Length];
static Vector2Int _mouseAxial;
static int _mouseHex;
static bool _mouseHexChanged;

static Board board => Cl.game.board;
static Pawn pawn => Cl.game.pawn;
static Game game => Cl.game;

static bool CanClick => QUI.hotWidget == 0 && QUI.activeWidget == 0;

static MapEditor() {
    MethodInfo [] methods = typeof( MapEditor ).GetMethods( Cellophane.BFS );
    foreach ( MethodInfo mi in methods ) {
        if ( mi.Name.EndsWith( "_tck" ) ) {
            for ( int i = 0; i < _ticks.Length; i++ ) {
                if ( _ticks[i].GetHashCode() == mi.GetHashCode() ) {
                    var nm = mi.Name.Remove( mi.Name.Length - 4 );
                    _tickNames[i] = Cellophane.NormalizeName( nm );
                }
            }
        }
    }
}

public static void Tick() {
    Vector2Int axial = Draw.ScreenToAxial( Cl.mousePosition );
    _mouseHexChanged = ( _mouseAxial - axial ).sqrMagnitude > 0;
    _mouseAxial = axial;
    _mouseHex = board.Hex( axial );
    _ticks[State_cvar % _ticks.Length]();
}

static void TickBegin( float pawnsAlpha = 1, bool skipVoidHexes = false ) {
    Draw.FillScreen();
    Draw.CenterBoardOnScreen();
    Draw.Board( skipVoidHexes: skipVoidHexes );

    if ( pawnsAlpha > 0.0001f ) {
        pawn.UpdateFilters();
        game.RegisterIntoGrids();
        Draw.PawnSprites( skipModels: true, alpha: pawnsAlpha );
    }
}

static void TickEnd() {
    var wbox = Draw.wboxScreen.TopCenter( Draw.wboxScreen.W, 20 * Draw.pixelSize );
    var text = $"Editor State: {_stateName}";
    int size = Draw.pixelSize * 2 / 3;
    WBUI.QGLTextOutlined( text, wbox, color: Color.white, fontSize: size );
}

static void None_tck() {
}

static void PlaceTerrain_tck() {
    TickBegin( pawnsAlpha: 0.2f );

    if ( ! CanClick ) {
        return;
    }

    if ( Cl.mouse0Down || ( Cl.mouse0Held && _mouseHexChanged && Cl.AllowSpam() ) ) {
        Vector2Int axial = Draw.ScreenToAxial( Cl.mousePosition );
        Cl.SvCmd( $"sv_set_terrain {axial.x} {axial.y} 128" );
    }

    if ( Cl.mouse1Down || ( Cl.mouse1Held && _mouseHexChanged && Cl.AllowSpam() ) ) {
        Vector2Int axial = Draw.ScreenToAxial( Cl.mousePosition );
        Cl.SvCmd( $"sv_set_terrain {axial.x} {axial.y} 0" );
    }

    TickEnd();
}

static void PlaceTowers_tck() {
    TickBegin();

    if ( ! CanClick ) {
        return;
    }

    if ( Cl.mouse0Down ) {
        if ( ! game.BoardHasDef( _mouseHex, Pawn.Tower ) ) { 
            Vector2 v = game.HexToV( _mouseHex );
            Cl.SvCmd( $"sv_spawn tower {Cellophane.FtoA( v.x )} {Cellophane.FtoA( v.y )}" );
        }
    }

    if ( Cl.mouse1Down ) {
        if ( game.GetPawnsOnHex( _mouseHex, out List<byte> l ) ) {
            foreach ( var z in l ) {
                if ( pawn.IsStructure( z ) ) {
                    Cl.SvCmd( $"sv_kill {z}" );
                }
            }
        }
    }

    TickEnd();
}

static bool CanReach( int hxA, int hxB, byte [] navMap ) {
    if ( hxA == hxB ) {
        return true;
    }

    bool isBlocking( Vector2Int axial ) {
        return navMap[board.Hex( axial )] != 0;
    }

    Vector3 cubeA = Hexes.AxialToCube( board.Axial( hxA ) );
    Vector3 cubeB = Hexes.AxialToCube( board.Axial( hxB ) );
    float n = Hexes.CubeDistance( cubeA, cubeB );
    float step = 1f / n;
    for ( float i = 0; i <= n; i++ ) {
        Vector3 c = Vector3.Lerp( cubeA, cubeB, i * step );
        Vector3 cr = Hexes.CubeRound( c );
        Vector3 d = c - cr;

        {
            Vector2Int ax = Hexes.CubeToAxial( cr );
            if ( isBlocking( ax ) ) {
                return false;
            }
        }

        const float eps = 0.49f;
        if ( d.x > eps * eps ) {
            c.x += 0.5f;
            Vector2Int ax = Hexes.CubeToAxial( Hexes.CubeRound( c ) );
            if ( isBlocking( ax ) ) {
                return false;
            }
        } else if ( d.y > eps * eps ) {
            c.y += 0.5f;
            Vector2Int ax = Hexes.CubeToAxial( Hexes.CubeRound( c ) );
            if ( isBlocking( ax ) ) {
                return false;
            }
        } else if ( d.z > eps * eps ) {
            c.z += 0.5f;
            Vector2Int ax = Hexes.CubeToAxial( Hexes.CubeRound( c ) );
            if ( isBlocking( ax ) ) {
                return false;
            }
        }
    }
    return true;
}

static int _hxA, _hxB;
static HexPather.Context _patherCTX => board.patherCTX;
static byte [] _navMap => board.navMap;
static List<int> _path => board.path;
static List<Vector2> _pathLine = new List<Vector2>();
static void PatherTest_tck() {
    TickBegin( pawnsAlpha: 0 );

    _hxB = Draw.ScreenToHex( Cl.mousePosition );
    if ( Cl.mouse0Down ) {
        _hxA = Draw.ScreenToHex( Cl.mousePosition );
        Array.Clear( _navMap, 0, board.numItems );
        foreach ( var hx in board.filter.no_solid ) {
            _navMap[hx] = 1;
        }
        _navMap[_hxA] = 0;
        HexPather.FloodMap( _hxA, 256, board.width, _navMap, board.numItems, _patherCTX );
        foreach ( var hx in board.filter.solid ) {
#if false
            Vector2Int a = board.Axial( _hxA );
            Vector2Int b = board.Axial( hx );
            _patherCTX.floodMap[hx] |= Hexes.AxialDistance( a, b ) << 8;
#else
            Vector3Int a = Hexes.AxialToCubeInt( board.Axial( _hxA ) );
            Vector3Int b = Hexes.AxialToCubeInt( board.Axial( hx ) );
            int dist = ( b - a ).sqrMagnitude;
            _patherCTX.floodMap[hx] = ( _patherCTX.floodMap[hx] << 16 ) | dist;
#endif
        }
    }

#if false
    foreach ( var hx in board.filter.solid ) {
        Vector2 spos = Draw.HexToScreen( hx );
        int dist = _patherCTX.floodMap[hx] >> 8;
        QGL.LatePrint( dist, spos );
    }
#endif

    foreach ( var hx in board.filter.solid ) {
        Vector2 spos = Draw.HexToScreen( hx );
        int dist = _patherCTX.floodMap[hx] & 0xffff;
        QGL.LatePrint( dist, spos );
    }

    HexPather.TracePath( _hxB, board.width, _patherCTX, _path );

    Color nodesColor = Color.cyan;
    nodesColor.a = 0.4f;
    foreach ( var hx in _path ) {
        Draw.TerrainTile( hx, c: nodesColor, sz: 0.5f );
    }

    Draw.TerrainTile( _hxA, c: Color.cyan, sz: 0.75f );
    Draw.TerrainTile( _hxB, c: Color.yellow, sz: 0.75f );

#if false
    var pth = new List<int>();

    if ( _path.Count > 2 ) {
        pth.Add( _path[0] );
        if ( CanReach( _path[0], _path[_path.Count - 1], _navMap ) ) {
            pth.Add( _path[_path.Count - 1] );
        } else {
            int start = 0;

            while ( true ) {
                int reach = 0;
                for ( int i = start + 1; i < _path.Count; i++ ) {
                    if ( CanReach( _path[start], _path[i], _navMap ) ) {
                        if ( CanReach( _path[i], _path[_path.Count - 1], _navMap ) ) {
                            pth.Add( _path[i] );
                            reach = _path.Count - 1;
                            break;
                        }
                        reach = i;
                    }
                }
                if ( reach > 0 ) {
                    pth.Add( _path[reach] );
                    start = reach;
                    if ( start == _path.Count - 1 ) {
                        break;
                    }
                }
            }
        }
    }
#endif

    board.StripPath();

    _pathLine.Clear();
    foreach ( var hx in board.strippedPath ) {
        _pathLine.Add( Draw.HexToScreen( hx ) );
    }
    QGL.LateDrawLine( _pathLine );

    TickEnd();
}

static bool HexTracingVariant_cvar = false;
static void HexTracing_tck() {
    TickBegin( pawnsAlpha: 0, skipVoidHexes: true );

    _hxB = Draw.ScreenToHex( Cl.mousePosition );

    if ( _hxA == 0 ) {
        if ( Cl.mouse0Down ) {
            _hxA = Draw.ScreenToHex( Cl.mousePosition );
        }
    }

    if ( Cl.mouse1Down ) {
        _hxA = 0;
    }

    if ( _hxA != 0 ) {
        Color col = Color.green;
        if ( HexTracingVariant_cvar ) {
            Vector3 cubeA = Hexes.AxialToCube( board.Axial( _hxA ) );
            Vector3 cubeB = Hexes.AxialToCube( board.Axial( _hxB ) );
            float n = Hexes.CubeDistance( cubeA, cubeB );
            float step = 1f / n;
            for ( float i = 0; i <= n; i++ ) {
                Vector3 c = Vector3.Lerp( cubeA, cubeB, i * step );
                Vector3 cr = Hexes.CubeRound( c );
                Vector3 d = c - cr;

                {
                    Vector2Int ax = Hexes.CubeToAxial( cr );
                    if ( ! board.IsSolid( ax ) ) {
                        col = Color.red;
                    }
                    Draw.TerrainTile( ax, c: col, sz: 0.5f );
                }

                const float eps = 0.49f;
                if ( d.x > eps * eps ) {
                    c.x += 0.5f;
                    Vector2Int ax = Hexes.CubeToAxial( Hexes.CubeRound( c ) );
                    if ( ! board.IsSolid( ax ) ) {
                        col = Color.red;
                    }
                    Draw.TerrainTile( ax, c: col, sz: 0.5f );
                } else if ( d.y > eps * eps ) {
                    c.y += 0.5f;
                    Vector2Int ax = Hexes.CubeToAxial( Hexes.CubeRound( c ) );
                    if ( ! board.IsSolid( ax ) ) {
                        col = Color.red;
                    }
                    Draw.TerrainTile( ax, c: col, sz: 0.5f );
                } else if ( d.z > eps * eps ) {
                    c.z += 0.5f;
                    Vector2Int ax = Hexes.CubeToAxial( Hexes.CubeRound( c ) );
                    if ( ! board.IsSolid( ax ) ) {
                        col = Color.red;
                    }
                    Draw.TerrainTile( ax, c: col, sz: 0.5f );
                }
            }
        } else {
            Vector3 cubeA = Hexes.AxialToCube( board.Axial( _hxA ) );
            Vector3 cubeB = Hexes.AxialToCube( board.Axial( _hxB ) );
            float n = Hexes.CubeDistance( cubeA, cubeB );
            float step = 1f / n;
            for ( float i = 0; i <= n; i++ ) {
                Vector3 cr = Hexes.CubeRound( Vector3.Lerp( cubeA, cubeB, i * step ) );
                Vector2Int ax = Hexes.CubeToAxial( cr );
                Draw.TerrainTile( ax, c: col, sz: 0.7f );
            }
        }

        QGL.LateDrawLine( Draw.HexToScreen( _hxA ), Draw.HexToScreen( _hxB ) );
    }

    Draw.TerrainTile( _hxA, c: Color.cyan, sz: 0.75f );
    Draw.TerrainTile( _hxB, c: Color.yellow, sz: 0.75f );

    TickEnd();
}

static void SetState_cmd( string [] argv ) {
    TickUtil.SetState( argv, _ticks, _tickNames, ref State_cvar );
}

static void Save_cmd( string [] argv ) {
    if ( argv.Length >= 2 ) {
        LastSavedMap_cvar = argv[1];
        if ( ! LastSavedMap_cvar.EndsWith( ".map" ) ) {
            LastSavedMap_cvar += ".map";
        }
    }
    Cl.SvCmd( $"sv_save_map {LastSavedMap_cvar}" );
}

static void Load_cmd( string [] argv ) {
    if ( argv.Length >= 2 ) {
        LastSavedMap_cvar = argv[1];
        if ( ! LastSavedMap_cvar.EndsWith( ".map" ) ) {
            LastSavedMap_cvar += ".map";
        }
    }
    Cl.SvCmd( $"sv_load_map {LastSavedMap_cvar}" );
}


}
