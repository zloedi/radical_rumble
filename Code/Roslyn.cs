/*
The unity editor uses a borked version of System.Runtime.Loader.
and throws NotImplementedException: The method or operation is not implemented
A workaround is to find the assembly in the editor binaries and replace it from the one 
supplied i.e. by the NuGet package with same/similar? name
*/

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

using UnityEngine;

namespace RR {

public static class Roslyn {
    public static Action<object> Log = o => {};
    public static Action<object> Error = o => {};

    static ScriptOptions _options;

    public static void Init() {
#if false
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach ( var a in assemblies ) {
            Log( a.Location );
        }
#endif

        _options = ScriptOptions.Default;
        if (Application.isEditor ) {
            var dir = $"{Application.dataPath}/../../BuildUnity/RadicalRumble_Data/Managed/";

            _options = _options.AddReferences(
                Assembly.LoadFrom( $"{dir}game.dll" ),
                Assembly.LoadFrom( $"{dir}UnityEngine.dll" )
            );
        } else {
            _options = _options.AddReferences(
                Assembly.LoadFrom( $"{Application.dataPath}/Managed/game.dll" )
            );
        }

        SetupWatcher();
    }

    static async Task CompiledRoslyn() {
        await _roslyn.RunAsync();
    }

    static void SetupWatcher() {
        var watcher = new FileSystemWatcher(@"c:\cygwin64\tmp\roslyn");

        watcher.NotifyFilter = NotifyFilters.Attributes
                                //| NotifyFilters.CreationTime
                                //| NotifyFilters.DirectoryName
                                //| NotifyFilters.FileName
                                //| NotifyFilters.LastAccess
                                | NotifyFilters.LastWrite
                                //| NotifyFilters.Security
                                | NotifyFilters.Size;

        watcher.Changed += OnChanged;
        //watcher.Created += OnCreated;
        //watcher.Deleted += OnDeleted;
        //watcher.Renamed += OnRenamed;
        watcher.Error += OnError;

        watcher.Filter = "*.cs";
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;
        Log( "Watcher setup done." );
    }

    static Script _roslyn;
    private static void OnChanged( object sender, FileSystemEventArgs e ) {
        Log( "Script changed: " + e.FullPath );
        if (e.ChangeType != WatcherChangeTypes.Changed) {
            return;
        }
        try {
            var code = File.ReadAllText( e.FullPath );
            _roslyn = CSharpScript.Create( code, _options );
        } catch ( Exception ex ) {
            Error( ex );
        }
    }

    private static void OnError(object sender, ErrorEventArgs e) {
        Error( e.GetException() );
    }

    static void RoslynScript_kmd( string [] _ ) {
        if ( _roslyn != null ) {
            try {
                CompiledRoslyn().Wait();
            } catch ( Exception ex ) {
                Error( ex );
                _roslyn = null;
            }
        }
    }
}

}
