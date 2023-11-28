using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

using static PawnDef.Flags;

public static class PawnDef {

public static Def Archer = Register( new Def {
    range = 4,
    maxHP = 20,
    speed = 0.1f,
    damage = 1,
} );

public static Def Brute = Register( new Def {
    maxHP = 20,
    speed = 0.1f,
    damage = 1,
} );

public static Def Flyer = Register( new Def {
    flags = Flying,
    maxHP = 20,
    speed = 0.1f,
    damage = 1,
} );

public static Def Tower = Register( new Def {
    range = 10,
    maxHP = 200,
    damage = 2,
} );

// =============================

public enum Flags {
    Flying,
}

public class Def {
    public string name;
    public Flags flags;
    public int maxHP;
    public float speed;
    public float range;
    public float damage;
}

public static List<Def> defs;

static PawnDef() {
    FieldInfo [] fields = typeof( PawnDef ).GetFields();
    foreach ( FieldInfo fi in fields ) {
        if ( fi.FieldType == typeof( Def ) ) {
            var def = fi.GetValue( null ) as Def;
            def.name = fi.Name;
        }
    }
}

public static bool FindIdxByName( string name, out int defIdx ) {
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

public static bool FindByName( string name, out Def def ) {
    if ( FindIdxByName( name, out int idx ) ) {
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
