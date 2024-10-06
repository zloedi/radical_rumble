#if false

/*
The unity editor uses a borked version of System.Runtime.Loader.
and throws NotImplementedException: The method or operation is not implemented
A workaround is to find the assembly in the editor binaries and replace it from the one 
supplied i.e. by the NuGet package with same/similar? name
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;

using UnityEngine;

namespace RR {

public static class Roslyn {
    public static Action<object> Log = o => {};
    public static Action<object> Error = o => {};
    public static Assembly gameAssembly;
    public static ScriptOptions options;

    //static Script _a, _b;

    public static void Init() {
        gameAssembly = typeof( Qonsole ).Assembly;

        options = ScriptOptions.Default;
        if (Application.isEditor ) {
            ////CompileString( @"public static class xx { void print() => Qonsole.Log( ""zloedi"" ) }", out Script a );
            //CompileString( @"public class xx { int ab; }", out Script a );
            ////CompileString( @"xx.print()", out Script b );
            //GetAssemblyFromScript( a, out Assembly asmA );
            ////GetAssemblyFromScript( b, out Assembly asmB );

            //var dir = $"{Application.dataPath}/../../BuildUnity/RadicalRumble_Data/Managed/";
            //var assemblies = new Assembly[] {
            //    asmA,
            //    //asmB,
            //    // FIXME: just use the ones in this app domain
            //    Assembly.LoadFrom( $"{dir}game.dll" ),
            //    Assembly.LoadFrom( $"{dir}UnityEngine.dll" )
            //};
            //options = options.AddReferences( assemblies );
        } else {
            options = options.AddReferences( gameAssembly );

            // use 'using static Script' (this class contains the compiled stuff)
            //CompileString( @"public static class xx { public static void print() => Qonsole.Log( ""xx"" ); }", out _a );
            //CompileString( @"public static class yy { public static void print() => Qonsole.Log( ""yy"" ); }", out _b );

            //GetAssemblyFromScript( _a, className: "ScriptA", out Assembly asmA );
            //GetAssemblyFromScript( _b, className: "ScriptB", out Assembly asmB );

            //var assemblies = new Assembly[] {
            //    asmA,
            //    asmB,
            //};

            //options = options.AddReferences( assemblies );
        }

        SetupWatcher();
    }

    public static bool CompileFileToScript( string path, out Script script ) {
        try {
            var code = File.ReadAllText( path );
            return CompileStringToScript( code, out script );
        } catch ( Exception ex ) {
            Error( ex );
            script = null;
            return false;
        }
    }

    public static bool CompileStringToScript( string code, out Script script ) {
        try {
            script = CSharpScript.Create( code, options );
            return true;
        } catch ( Exception ex ) {
            Error( ex );
            script = null;
            return false;
        }
    }

    //static void ReloadAssemblies_kmd( string [] _ ) {
    //    GetAssemblyFromScript( _a, className: "ScriptA", out Assembly asmA );
    //    GetAssemblyFromScript( _b, className: "ScriptB", out Assembly asmB );
    //}

    static async Task CompiledRoslyn() => await _roslyn.RunAsync();

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

    static bool GetAssemblyFromScript( Script script, string className, out Assembly assembly ) {
        Log( "get asm from script..." );
        assembly = null;
        var compilationOptions = new CSharpCompilationOptions( OutputKind.DynamicallyLinkedLibrary, 
                                                                       scriptClassName: className );
        var compilation = script.GetCompilation().WithOptions( compilationOptions );
        byte[] scriptAssemblyBytes;
        using ( var ms = new MemoryStream() ) {
            var result = compilation.Emit( ms );
            if ( ! result.Success ) {
                Error( "Couldn't emit compilation." );
                foreach ( var d in result.Diagnostics ) {
                    Error( d );
                }
                return false;
            }
            ms.Seek( 0, SeekOrigin.Begin );
            scriptAssemblyBytes = ms.ToArray();
        }

        string dir = @"c:\cygwin64\tmp\roslyn\";
        string name = Guid.NewGuid().ToString().Substring( 0, 8 );
        string path = $"{dir}{name}.dll";

        try {
            File.WriteAllBytes( path, scriptAssemblyBytes );
            Log( $"Saved '{path}'" );
            // the roslyn ScriptOptions refs need Assembly.Location, thus the write/read from file...
            assembly = Assembly.LoadFrom( path );
            //assembly = Assembly.Load( scriptAssemblyBytes );
            Log( "Compiled assembly " + assembly.GetName() );
        } catch ( Exception ) {
            Error( $"Failed to compile '{path}'" );
        }

        return assembly != null;
    }

    static Script _roslyn;
    static void OnChanged( object sender, FileSystemEventArgs e ) {
        Log( "Script changed: " + e.FullPath );
        if (e.ChangeType != WatcherChangeTypes.Changed) {
            return;
        }
        CompileFileToScript( e.FullPath, out _roslyn );
    }

    static void OnError(object sender, ErrorEventArgs e) {
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

    /*

    compile multiple c# files into 'new' assembly

    downsides:
        you need to hack your dll-s
        you need to structure your programs in a cerain way
            mono behaviours will kill you
        keeps garbagbe around

    the goals:
        * 1. compile the entire 'unity player' part of the client from scripts
            ClientPlayUnity.cs
            GUIUnity.cs
        2. add qonsole hooks for scripts i.e.
            client-tick-prolog, 
            client-tick-epilog,
            client-draw-prolog
            client-draw-epilog
            client-incoming-packet
            etc.
    */

}

}

#endif
