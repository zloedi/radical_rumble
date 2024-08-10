using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public static class FilterUtil {


// use reflection to create and collect all lists
public static void CreateAll( object o, out List<IList> all ) {
    all = new List<IList>();
    FieldInfo [] fields = o.GetType().GetFields();
    foreach ( FieldInfo fi in fields ) {
        Type ft = fi.FieldType;
        if( ft.IsGenericType
                && ft.GetGenericTypeDefinition() == typeof( List<> )
                && ft != typeof( List<IList> ) ) {
            var list = Activator.CreateInstance( ft );
            fi.SetValue( o, list );
            all.Add( ( IList )list );
        }
        if( ft.IsArray ) {
            ft = fi.FieldType.GetElementType();
            if( ft.IsGenericType
                    && ft.GetGenericTypeDefinition() == typeof( List<> )
                    && ft != typeof( List<IList> ) ) {
                Array a = ( Array )fi.GetValue( o );
                for ( int i = 0; i < a.Length; i++ ) {
                    var list = Activator.CreateInstance( ft );
                    a.SetValue( list, i );
                    all.Add( ( IList )list );
                }
            }
        }
    }
}


public static List<T>[] ArrayOfLists<T>(int arraySize, List<IList> all) {
    var array = new List<T>[arraySize];
    for ( int i = 0; i < array.Length; i++ ) {
        array[i] = new List<T>();
        all.Add( array[i] );
    }
    return array;
}


}
