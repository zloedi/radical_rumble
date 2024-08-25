using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

#if UNITY_STANDALONE
using UnityEngine;
#else
using GalliumMath;
#endif

namespace RR {


using static Pawn.Def.Flags;


partial class Pawn {

public static Def Tower = Register( new Def {
    flags = Structure | PatrolWaypoint | WinObjective,
    range = 7,
    maxHP = 2400,
    damage = 60,
    radius = 1.4f,
    color = Color.magenta,
    u_healthbar = 3,

    description = "A towering stone structure, its walls are thick and fortified with iron spikes. The tower provides a strategic vantage point and a powerful deterrent to any would-be attackers.",

} );

public static Def Turret = Register( new Def {
    attackTime = 800,
    loadTime = 300,
    damage = 50,

    flags = Structure,
    range = 6,
    maxHP = 1400,
    radius = 1f,
    color = new Color( 1f, 0.8f, 0 ),
    symbol = 'U',
    u_healthbar = 2,

    description = "A massive, wooden ballista stands tall. Its massive bolts are tipped with sharp metal points, capable of piercing even the hardest enemy. The ballista is a fearsome weapon of war, capable of turning the tide of even the most desperate battle.",
} );

public static Def Brute = Register( new Def {
    attackTime = 1200,
    loadTime = 800,
    damage = 80,
    speed = 60,

    color = Color.green,
    maxHP = 500,
    radius = 0.4f,

    animIdle = 5,
    animMove = 6,
    animAttack = 7,
    u_healthbar = 1,

    description = "A towering knight, clad in heavy plate armor, wields a massive sword. His muscles bulge beneath his armor. He is able to crush even the toughest opponents. However, his heavy armor limits his speed and agility, making him vulnerable to ranged attacks.",

} );

public static Def Archer = Register( new Def {
    attackTime = 900,
    loadTime = 400,
    damage = 40,
    range = 5,
    count = 2,

    color = Color.red,
    maxHP = 120,
    radius = 0.35f,
    symbol = 'a',

    description = "A seasoned archer, clad in leather armor, wields a longbow. Her eyes are sharp and focused, and her movements are precise. She possesses incredible accuracy, able to hit targets from incredible distances. Her arrows are crafted with the finest materials, ensuring a deadly impact upon their target.",

} );

public static Def Flyer = Register( new Def {
    attackTime = 2000,
    loadTime = 1700,
    damage = 400,
    speed = 45,

    cost = 5,

    color = new Color( 0f, 0.4f, 1f ),
    flags = Flying,
    maxHP = 1000,
    radius = 0.6f,

    animIdle = 9,
    animMove = 7,
    animAttack = 6,

    momentAttackOut = 0.7f,

    u_healthbar = 1,
    u_healthbarOffset = new Vector3( 0, 3.5f, 0 ),

    description = "A massive, winged beast dominates the skies. Its armored scales and immense size make it nearly invincible. Its powerful wings can generate hurricane-force winds.",

} );

public static Def Zombie = Register( new Def {
    attackTime = 900,
    loadTime = 400,
    damage = 40,
    speed = 90, // slow = 45, medium = 60, fast = 90, very fast = 120 (tiles per minute)

    cost = 1,
    count = 5,
    radius = 0.32f,
    symbol = 'z',

    color = new Color( 0.9f, 0.9f, 0.9f ),
    maxHP = 40,

    animIdle = 5,
    animMove = 6,
    animAttack = 7,

    description = "A skeletal figure, clad in a tattered cloak. Its empty eye sockets are filled with a cold, calculating gaze. Despite its weak appearance, it possesses incredible speed and agility.",

} );

// =============================

public class Def {
    [Flags]
    public enum Flags {
        None,
        Structure       = 1 << 0,
        Flying          = 1 << 1,
        PatrolWaypoint  = 1 << 2,
        WinObjective    = 1 << 3,
    }

    public const int MAX_HEALTH_BAR = 4;

    public static float MaxRadius;

    public bool IsStructure => ( flags & Flags.Structure ) != 0;

    public string name;
    public int count = 1;
    public char symbol = ' ';
    public Flags flags;
    public int cost = 3;
    public int maxHP;
    public int speed = 60;
    // total attack loop time
    public int attackTime = 1000;
    // part of the attack loop spent in 'reloading'
    // if it has not passed, won't connect i.e. on death
    public int loadTime = 600;
    public float range;
    public int damage;
    public float radius = 0.5f;
    public Color color;

    // Animo states
    // anim names starting with 'anim' and are of type 'int' (to fill the dict with reflection)
    public int animIdle = 4;
    public int animMove = 5;
    public int animAttack = 6;
    public Dictionary<string,int> anims = new Dictionary<string,int>();

    // animation event moments
    public float momentLandHit = 0.3f;
    public float momentAttackOut = 1f;

    public string description;

    // Unity healthbar size
    public int u_healthbar = 0;
    // Unity healthbar offset from origin
    public Vector3 u_healthbarOffset = new Vector3( 0, 2.5f, 0 );
}

public static List<Def> defs;

void FillDefReflection() {
    FieldInfo [] fields = typeof( Pawn ).GetFields();
    foreach ( FieldInfo fi in fields ) {
        if ( fi.FieldType == typeof( Def ) ) {
            var def = fi.GetValue( null ) as Def;
            def.name = fi.Name;
            def.symbol = def.symbol == ' ' ? def.name[0] : def.symbol;

            FieldInfo [] ffields = typeof( Def ).GetFields();
            foreach ( FieldInfo ffi in ffields ) {
                if ( ffi.FieldType == typeof( int ) && ffi.Name.StartsWith( "anim" ) ) {
                    def.anims[ffi.Name.Substring( 4 )] = ( int )ffi.GetValue( def );
                }
            }
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
        var dummy = new Def();
        dummy.name = "GARBAGE";
        dummy.description = "A towering pile of refuse, its stench is overpowering and its appearance is unsightly.";
        defs = new List<Def>{ dummy };
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


} }
