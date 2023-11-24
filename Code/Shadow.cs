using System;
using System.Collections.Generic;
using System.Reflection;

public class Shadow {

public enum DeltaType {
    None,
    Uint8,
    Uint16,
    Int32,
}

public class Row {
    public object parentObject;
    public string name;
    public int maxRow;
    public DeltaType type;
    public Array array;
}

public Action<string> Log = s => {};
public Action<string> Error = s => {};

// row name to actual row
public Dictionary<string,Array> nameToArray = new Dictionary<string,Array>();
// row to shadow map
public Dictionary<Array,Row> arrayToShadow = new Dictionary<Array,Row>();

// pass down an object of rows
public bool CreateShadows( object obj, int maxRow = 0, bool skipClone = false ) {
    FieldInfo [] fields = obj.GetType().GetFields();

    foreach ( FieldInfo fi in fields ) {
        Array row = fi.GetValue( obj ) as Array;

        if ( row == null ) {
            continue;
        }

        if ( arrayToShadow.TryGetValue( row, out Row shadow ) ) {
            continue;
        }

        var nameKey = $"{obj.GetType().Name}_{fi.Name}";

        DeltaType deltaType;
        Type elemType = fi.FieldType.GetElementType();

        if ( elemType == typeof( byte ) ) {
            deltaType = DeltaType.Uint8;
        } else if ( elemType == typeof( ushort ) ) {
            deltaType = DeltaType.Uint16;
        } else if ( elemType == typeof( int ) ) {
            deltaType = DeltaType.Int32;
        } else {
            Error( $"Unknown DeltaType {elemType} on field {nameKey}" );
            return false;
        }

        nameToArray[nameKey] = row;

        arrayToShadow[row] = new Row {
            parentObject = obj,
            name = nameKey,
            type = deltaType,
            maxRow = maxRow,
        };

        if ( ! skipClone ) {
            arrayToShadow[row].array = ( Array )row.Clone();
        }

        Log( $"Created shadow buffer {nameKey}" );
    }

    return true;
}


}
