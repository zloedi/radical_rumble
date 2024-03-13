using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

using Cl = RRClient;

static class MapEditor {


static string EdLastSavedMap_kvar = "unnamed";

static int EdState_kvar = 1;
static bool EdHexTracingVariant_kvar = false;

static string [] _tickNames;
static Action [] _ticks = TickUtil.RegisterTicksOfClass( typeof( MapEditor ), out _tickNames );

static string _stateName => _tickNames[EdState_kvar % _ticks.Length];

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
    int t = EdState_kvar % _ticks.Length;
    Cl.TickKeybinds( context: $"ed_{_tickNames[t]}" );
    _ticks[t]();
}

static void TickBegin( float pawnsAlpha = 1, bool skipVoidHexes = false ) {
    Draw.FillScreen();
    Draw.CenterBoardOnScreen();
    Draw.Board( skipVoidHexes: skipVoidHexes );

    if ( pawnsAlpha > 0.0001f ) {
        pawn.UpdateFilters();
        game.RegisterIntoGrids();
        foreach ( var z in pawn.filter.no_garbage ) {
            pawn.mvPos[z] = pawn.mvEnd[z];
        }
        Draw.PawnSprites( alpha: pawnsAlpha );
    }
}

static void TickEnd() {
    var wbox = Draw.wboxScreen.BottomCenter( Draw.wboxScreen.W, 20 * Draw.pixelSize );
    var text = $"'{EdLastSavedMap_kvar}' {_stateName}";
    WBUI.QGLTextOutlined( text, wbox, color: Color.white, fontSize: Draw.textSize );
}

static void None_tck() {
}

static void PlaceTerrain_tck() {
    TickBegin( pawnsAlpha: 0.2f );

    if ( ! CanClick ) {
        return;
    }

    Vector2 spos = Draw.AxialToScreen( Cl.mousePosAxial );
    Hexes.DrawHexWithLines( spos, Draw.hexPixelSize, Color.white );

    if ( Cl.mouse0Down || ( Cl.mouse0Held && Cl.mouseHexChanged && Cl.AllowSpam() ) ) {
        Cl.SvCmd( $"sv_set_terrain {Cl.mousePosAxial.x} {Cl.mousePosAxial.y} 128" );
    }

    if ( Cl.mouse1Down || ( Cl.mouse1Held && Cl.mouseHexChanged && Cl.AllowSpam() ) ) {
        Cl.SvCmd( $"sv_set_terrain {Cl.mousePosAxial.x} {Cl.mousePosAxial.y} 0" );
    }

    TickEnd();
}

static void PlaceTowers_tck() {
    PlaceStructTick( "tower" );
}

static void PlaceTurrets_tck() {
    PlaceStructTick( "turret" );
}

static void PlaceStructTick( string structName ) {
    TickBegin();

    if ( ! CanClick ) {
        return;
    }

    if ( Cl.mouse0Down ) {
        if ( game.GetFirstPawnOnHex( Cl.mouseHex, out int z ) ) {
            Qonsole.OneShotCmd( $"ed_set_team {z} 0;" );
        } else {
            Vector2 v = game.HexToV( Cl.mouseHex );
            Cl.SvCmd( $"sv_spawn {structName} {Cellophane.FtoA( v.x )} {Cellophane.FtoA( v.y )}" );
        }
    }

    if ( Cl.mouse1Down ) {
        if ( game.GetPawnsOnHex( Cl.mouseHex, out List<byte> l ) ) {
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

    _hxB = Cl.mouseHex;
    if ( Cl.mouse0Down ) {
        _hxA = Cl.mouseHex;
        Array.Clear( _navMap, 0, board.numItems );
        foreach ( var hx in board.filter.no_solid ) {
            _navMap[hx] = 1;
        }
        _navMap[_hxA] = 0;
        HexPather.FloodMap( _hxA, 256, board.width, _navMap, board.numItems, _patherCTX );
        foreach ( var hx in board.filter.solid ) {
#if false
            _patherCTX.floodMap[hx] <<= 16;
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
        Vector3Int a = Hexes.AxialToCubeInt( board.Axial( _hxB ) );
        Vector3Int b = Hexes.AxialToCubeInt( board.Axial( hx ) );
        int dist = ( b - a ).sqrMagnitude;
        _patherCTX.floodMap[hx] = ( int )( _patherCTX.floodMap[hx] & 0xffff0000 );
        _patherCTX.floodMap[hx] |= dist;
    }
#endif

    foreach ( var hx in board.filter.solid ) {
        Vector2 spos = Draw.HexToScreen( hx );
        int score = _patherCTX.floodMap[hx] >> 16;
        int dist = _patherCTX.floodMap[hx] & 0xffff;
        QGL.LatePrint( $"{score}\n{dist}", spos, color: Color.white );
    }

    HexPather.TracePath( _hxB, board.width, _patherCTX, _path );

    Color nodesColor = Color.cyan;
    nodesColor.a = 0.4f;
    foreach ( var hx in _path ) {
        Draw.TerrainTile( hx, c: nodesColor, sz: 0.5f );
    }

    Draw.TerrainTile( _hxA, c: Color.cyan, sz: 0.75f );
    Draw.TerrainTile( _hxB, c: Color.yellow, sz: 0.75f );

    board.StripPath();

    _pathLine.Clear();
    foreach ( var hx in board.strippedPath ) {
        _pathLine.Add( Draw.HexToScreen( hx ) );
    }
    QGL.LateDrawLine( _pathLine );

    TickEnd();
}

static void HexTracing_tck() {
    TickBegin( pawnsAlpha: 0, skipVoidHexes: true );

    _hxB = Cl.mouseHex;

    if ( _hxA == 0 ) {
        if ( Cl.mouse0Down ) {
            _hxA = Cl.mouseHex;
        }
    }

    if ( Cl.mouse1Down ) {
        _hxA = 0;
    }

    if ( _hxA != 0 ) {
        Color col = Color.green;
        if ( EdHexTracingVariant_kvar ) {
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

// phony pawns to test the attack solver
static List<Vector2> _atkOrigin = new List<Vector2>();
static List<byte> _atkDef = new List<byte>();
static List<byte> _atkTeam = new List<byte>();
// the visuals circle
static Vector2 [] _atkCircle = new Vector2[14];
static void AtkPosSolver_tck() {
    TickBegin( pawnsAlpha: 0, skipVoidHexes: true );

    void draw( int n ) {
        int max = _atkCircle.Length;
        float step = ( float )( Math.PI * 2f / max );
        Vector2 origin = Draw.GTS( _atkOrigin[n] );
        float r = Pawn.defs[_atkDef[n]].radius * Draw.hexPixelSize;
        for ( int i = 0; i < max; i++ ) {
            Vector2 v = new Vector2( Mathf.Cos( i * step ), Mathf.Sin( i * step ) );
            _atkCircle[i] = v * r + origin;
        }
        var c = _atkTeam[n] == 0 ? Color.cyan : Color.red;
        QGL.LateDrawLineLoop( _atkCircle, color: c );
    }

    for ( int i = 0; i < _atkOrigin.Count; i++ ) {
        draw( i );
    }

    TickEnd();
}

static List<Vector2> _atkx = new List<Vector2>();
static List<float> _atkw = new List<float>();
static List<float> _atkr = new List<float>();
static void EdAtkPosSolverPlace_kmd( string [] argv ) {
    if ( argv.Length < 2 ) {
        Cl.Log( $"{argv[0]} <def> [team]" );
        return;
    }
    int.TryParse( argv[1], out int def );
    def = Mathf.Clamp( def, 1, Pawn.defs.Count - 1 );
    _atkOrigin.Add( Cl.mousePosGame );
    _atkDef.Add( ( byte )def );
    int team = 0;
    if ( argv.Length > 2 ) {
        int.TryParse( argv[2], out team );
    }
    _atkTeam.Add( ( byte )team );

    // push away any clipping pawns
#if false
    int numSubsteps = 8;
    for ( int i = 0; i < numSubsteps; i++ ) {
        for ( int z1 = 0; z1 < _atkOrigin.Count; z1++ ) {
            for ( int z2 = 0; z2 < _atkOrigin.Count; z2++ ) {
                if ( z1 == z2 ) {
                    continue;
                }

                Vector2 x1 = _atkOrigin[z1];
                Vector2 x2 = _atkOrigin[z2];

                Pawn.Def def1 = Pawn.defs[_atkDef[z1]];
                Pawn.Def def2 = Pawn.defs[_atkDef[z2]];

                float w1 = def1.IsStructure ? 0 : 1;
                float w2 = def2.IsStructure ? 0 : 1;

                float r1 = def1.radius;
                float r2 = def2.radius;

                // the desired (rest) distance, make sure we overshoot
                float l0 = r1 + r2 + 0.01f;
                // the actual distance
                float l = ( x2 - x1 ).magnitude;

                if ( l - ( r1 + r2 ) > 0.0001f ) {
                    continue;
                }

                if ( w1 + w2 < 0.0001f ) {
                    continue;
                }

                Vector2 dx1 = +w1 / ( w1 + w2 ) * ( l - l0 ) * ( x2 - x1 ) / ( x2 - x1 ).magnitude;
                Vector2 dx2 = -w2 / ( w1 + w2 ) * ( l - l0 ) * ( x2 - x1 ) / ( x2 - x1 ).magnitude;

                _atkOrigin[z1] += dx1;
                _atkOrigin[z2] += dx2;
            }
        }
    }
#else
    _atkx.Clear();
    _atkw.Clear();
    _atkr.Clear();
    for ( int z = 0; z < _atkOrigin.Count; z++ ) {
        Pawn.Def d = Pawn.defs[_atkDef[z]];
        _atkx.Add( _atkOrigin[z] );
        _atkw.Add( d.IsStructure ? 0 : 1 );
        _atkr.Add( d.radius + 0.05f );
    }
    Gym.SolveOverlapping( _atkx, _atkw, _atkr );
    for ( int z = 0; z < _atkOrigin.Count; z++ ) {
        _atkOrigin[z] = _atkx[z];
    }
#endif

    Qonsole.Log( $"Placed pawn r:{Pawn.defs[def].radius} at {Cl.mousePosGame.x} {Cl.mousePosGame.y}" );
}

static void EdAtkPosSolverRemove_kmd( string [] argv ) {
    for ( int i = 0; i < _atkOrigin.Count; i++ ) {
        var o = _atkOrigin[i];
        if ( ( o - Cl.mousePosGame ).sqrMagnitude <= 0.25f ) {
            _atkOrigin.RemoveAt( i );
            _atkDef.RemoveAt( i );
            _atkTeam.RemoveAt( i );
            Qonsole.Log( $"Removed pawn at {o.x} {o.y}" );
            break;
        }
    }
}

static void EdSetTeam_kmd( string [] argv ) {
    if ( argv.Length < 3 ) {
        Cl.Log( $"{argv[0]} <z> <team>" );
        return;
    }
    int.TryParse( argv[1], out int z );
    int.TryParse( argv[2], out int team );
    Cl.Log( $"Setting team on {z} to {team}..." );
    Cl.SvCmd( $"sv_set_team {z} {team}" );
}

static void EdSetState_kmd( string [] argv ) {
    TickUtil.SetState( argv, _ticks, _tickNames, ref EdState_kvar );
}

static void EdSave_kmd( string [] argv ) {
    if ( argv.Length >= 2 ) {
        EdLastSavedMap_kvar = argv[1];
        if ( ! EdLastSavedMap_kvar.EndsWith( ".map" ) ) {
            EdLastSavedMap_kvar += ".map";
        }
    }
    Cl.SvCmd( $"sv_save_map {EdLastSavedMap_kvar}" );
}

static void EdLoad_kmd( string [] argv ) {
    if ( argv.Length >= 2 ) {
        EdLastSavedMap_kvar = argv[1];
        if ( ! EdLastSavedMap_kvar.EndsWith( ".map" ) ) {
            EdLastSavedMap_kvar += ".map";
        }
    }
    Cl.SvCmd( $"sv_load_map {EdLastSavedMap_kvar}" );
}


}
