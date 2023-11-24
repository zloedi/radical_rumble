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
        Type fldType = fi.FieldType;
        if( fldType.IsGenericType
                && fldType.GetGenericTypeDefinition() == typeof( List<> )
                && fldType != typeof( List<IList> ) ) {
            var list = Activator.CreateInstance( fldType );
            fi.SetValue( o, list );
            all.Add( ( IList )list );
        }
    }
}


}
