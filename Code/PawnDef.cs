using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using GalliumMath;

using static Pawn.Def.Flags;

partial class Pawn {

public static Def Tower = Register( new Def {
    flags = Structure | NavFocus,
    range = 10,
    maxHP = 5000,
    damage = 2,
    radius = 0.6f,
    color = Color.magenta,
} );

public static Def Turret = Register( new Def {
    flags = Structure,
    range = 10,
    maxHP = 5000,
    damage = 2,
    radius = 0.5f,
    color = new Color( 1f, 0.8f, 0 ),
    debugName = "U",
} );

public static Def Archer = Register( new Def {
    range = 4,
    maxHP = 500,
    speed = 120,
    damage = 1,
    radius = 0.4f,
    color = Color.red,
} );

public static Def Brute = Register( new Def {
    maxHP = 500,
    speed = 60,
    damage = 1,
    radius = 0.4f,
    color = Color.green,
} );

public static Def Flyer = Register( new Def {
    flags = Flying,
    maxHP = 500,
    speed = 60,
    damage = 1,
    radius = 0.5f,
    color = new Color( 0f, 0.4f, 1f ),
} );

// =============================

public class Def {
    [Flags]
    public enum Flags {
        None,
        Structure = 1 << 0,
        Flying    = 1 << 1,
        // FIXME: is this used?
        NavFocus  = 1 << 2,
    }

    public static float MaxRadius;

    public string name;
    public string debugName;
    public Flags flags;
    public int maxHP;
    public int speed = 60;
    public float range;
    public float damage;
    public float radius = 0.5f;
    public Color color;

    public bool IsStructure => ( flags & Flags.Structure ) != 0;
}

public static List<Def> defs;

void FillDefNames() {
    FieldInfo [] fields = typeof( Pawn ).GetFields();
    foreach ( FieldInfo fi in fields ) {
        if ( fi.FieldType == typeof( Def ) ) {
            var def = fi.GetValue( null ) as Def;
            def.name = fi.Name;
            def.debugName = def.debugName == null ? "" + def.name[0] : def.debugName;
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
    Def.MaxRadius = Mathf.Max( Def.MaxRadius, def.radius );
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
    Qonsole.Log( $"MaxRadius: {Def.MaxRadius}" );
}


}
