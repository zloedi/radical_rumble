using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public static class ArrayUtil {


// create arrays only at null fields
public static void CreateNulls( object o, int numElements, out List<Array> all ) {
    all = new List<Array>();
    FieldInfo [] fields = o.GetType().GetFields();
    foreach ( FieldInfo fi in fields ) {
        if ( fi.FieldType.IsArray && fi.GetValue( o ) == null ) {
            Array row = Array.CreateInstance( fi.FieldType.GetElementType(), numElements );
            fi.SetValue( o, row );
            all.Add( row );
        }
    }
}

// use reflection to create and collect all arrays
public static void CreateAll( object o, int numElements, out List<Array> all ) {
    all = new List<Array>();
    FieldInfo [] fields = o.GetType().GetFields();
    foreach ( FieldInfo fi in fields ) {
        if ( fi.IsInitOnly ) {
            continue;
        }
        if ( fi.FieldType.IsArray ) {
            Array row = Array.CreateInstance( fi.FieldType.GetElementType(), numElements );
            fi.SetValue( o, row );
            all.Add( row );
        }
    }
}

// no fucking number generic constraint
public static bool FindFreeColumn( int [] lookup, out int idx, int lastFree = 0 ) {
    if ( lastFree != 0 && lookup[lastFree] == 0 ) {
        idx = lastFree;
    } else {
        for ( idx = 1; idx < lookup.Length; idx++ ) {
            if ( lookup[idx] == 0 ) {
                break;
            }
        }
    }

    if ( idx == lookup.Length ) {
        idx = 0;
        return false;
    }

    return true;
}

public static bool FindFreeColumn( ushort [] lookup, out int idx, int lastFree = 0 ) {
    if ( lastFree != 0 && lookup[lastFree] == 0 ) {
        idx = lastFree;
    } else {
        for ( idx = 1; idx < lookup.Length; idx++ ) {
            if ( lookup[idx] == 0 ) {
                break;
            }
        }
    }

    if ( idx == lookup.Length ) {
        idx = 0;
        return false;
    }

    return true;
}

public static bool FindFreeColumn( byte [] lookup, out int idx, int lastFree = 0 ) {
    if ( lastFree != 0 && lookup[lastFree] == 0 ) {
        idx = lastFree;
    } else {
        for ( idx = 1; idx < lookup.Length; idx++ ) {
            if ( lookup[idx] == 0 ) {
                break;
            }
        }
    }

    if ( idx == lookup.Length ) {
        idx = 0;
        return false;
    }

    return true;
}

public static void ClearColumn( List<Array> all, int idx ) {
    foreach ( var r in all ) {
        Array.Clear( r, idx, 1 );
    }
}

public static void Clear( List<Array> all ) {
    foreach ( var r in all ) {
        Array.Clear( r, 0, r.Length );
    }
}


}
