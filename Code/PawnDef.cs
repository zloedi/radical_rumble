using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

using static Pawn.Def.Flags;

partial class Pawn {

public static Def Archer = Register( new Def {
    range = 4,
    maxHP = 20,
    speed = 120,
    damage = 1,
    color = Color.red,
} );

public static Def Brute = Register( new Def {
    maxHP = 20,
    speed = 60,
    damage = 1,
    color = Color.green,
} );

public static Def Flyer = Register( new Def {
    flags = Flying,
    maxHP = 20,
    speed = 60,
    damage = 1,
    color = new Color( 0f, 0.4f, 1f ),
} );

public static Def Tower = Register( new Def {
    flags = Structure,
    range = 10,
    maxHP = 200,
    damage = 2,
    color = Color.magenta,
} );

// =============================

public class Def {
    [Flags]
    public enum Flags {
        None,
        Structure = 1 << 0,
        Flying    = 1 << 1,
    }

    public string name;
    public Flags flags;
    public int maxHP;
    public int speed;
    public float range;
    public float damage;
    public Color color;
}

public static List<Def> defs;

void FillDefNames() {
    FieldInfo [] fields = typeof( Pawn ).GetFields();
    foreach ( FieldInfo fi in fields ) {
        if ( fi.FieldType == typeof( Def ) ) {
            var def = fi.GetValue( null ) as Def;
            def.name = fi.Name;
        }
    }
}

public static bool FindDefIdxByName( string name, out int defIdx ) {
    var nlw = name.ToLowerInvariant();
    for ( int i = 1; i < defs.Count; i++ ) {
        var d = defs[i];
        if ( d.name.ToLowerInvariant().Contains( nlw ) ) {
            defIdx = i;
            return true;
        }
    }
    defIdx = 0;
    return false;
}

public static bool FindDefByName( string name, out Def def ) {
    if ( FindDefIdxByName( name, out int idx ) ) {
        def = defs[idx];
        return true;
    }
    def = null;
    return false;
}

static Def Register( Def def ) {
    if ( defs == null ) {
        defs = new List<Def>{ new Def() };
    }
    defs.Add( def );
    return def;
}

static void PrintDefs_kmd( string [] argv ) {
    foreach ( var d in defs ) {
        FieldInfo [] fields = typeof( Def ).GetFields();
        foreach ( FieldInfo fi in fields ) {
            var o = fi.GetValue( d );
            Qonsole.Log( o );
        }
        Qonsole.Log( "\n" );
    }
}


}
