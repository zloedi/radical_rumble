using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using UnityEngine;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;

namespace RR {


public static class RoslynPlayer {


static int _reloads;
static Assembly _playerAssembly;
static Action _roslynTick = () => {};
static string _sourcesDir;

static string [] _scriptSources = {
    "GUIUnity.cs",
    "GUIEvent5.cs",

    // keep it last
    "ClientPlayUnity.cs",
};

public static void Init() {
    _sourcesDir = $"{Application.dataPath}/../../Code/";

    Roslyn.Log( $"Setup File Watcher to {_sourcesDir}" );

    var watcher = new FileSystemWatcher( _sourcesDir );
    watcher.Filter = "*.cs";
    watcher.NotifyFilter = NotifyFilters.LastWrite;

    watcher.Changed += OnScriptChanged;
    watcher.Error += OnScriptError;

    watcher.IncludeSubdirectories = false;
    watcher.EnableRaisingEvents = true;
}

static void OnScriptChanged( object sender, FileSystemEventArgs e ) {
    if ( e.ChangeType != WatcherChangeTypes.Changed ) {
        return;
    }

    if ( Array.IndexOf( _scriptSources, e.Name ) < 0 )
        return;

    Roslyn.Log( $"Script changed: {e.FullPath}, recompiling..." );

    var trees = new SyntaxTree[_scriptSources.Length];

    for ( int i = 0; i < _scriptSources.Length; i++ ) {
        string path = _sourcesDir + _scriptSources[i];
        if ( ! ParseFile( path, true, out trees[i] ) ) {
            return;
        }
        Roslyn.Log( $"Parsed {path}" );
    }

    if ( ! CompileSyntaxTrees( trees, out byte [] image ) ) {
        return;
    }

    try {
        _playerAssembly = Assembly.Load( image );
        Type t = _playerAssembly.GetType( "RR.ClientPlayUnity" );
        MethodInfo mi = t.GetMethod( "Tick" );
        _roslynTick = mi.CreateDelegate( typeof( Action ) ) as Action;
        _reloads++;
    } catch ( Exception ex ) {
        Roslyn.Error( ex );
    }
}

static void OnScriptError( object sender, ErrorEventArgs e ) {
    Roslyn.Error( e.GetException() );
}

static bool ParseFile( string path, bool dll, out SyntaxTree tree ) {
    try {
        tree = Parse( File.ReadAllText( path ) );
        return true;
    } catch ( Exception ex ) {
        tree = null;
        Roslyn.Error( ex );
        return false;
    }
}

static bool CompileSyntaxTrees( SyntaxTree [] trees, out byte [] image ) {
    try {
        image = null;

        var compilation = CreateCompilation( $"program_{_reloads}", trees,
             compilerOptions: new CSharpCompilationOptions( OutputKind.DynamicallyLinkedLibrary ) );

        using ( var ms = new MemoryStream() ) {
            var result = compilation.Emit( ms );
            if ( ! result.Success ) {
                Roslyn.Error( "Compilation failed." );
                foreach ( var d in result.Diagnostics ) {
                    Roslyn.Error( d );
                }
                return false;
            }
            ms.Seek( 0, SeekOrigin.Begin );
            image = ms.ToArray();
        }

        return true;

    } catch ( Exception ex ) {
        Roslyn.Error( ex );
        image = null;
        return false;
    }
}

static SyntaxTree Parse( string code ) {
    return SyntaxFactory.ParseSyntaxTree( code,
                options: CSharpParseOptions.Default.WithPreprocessorSymbols( "UNITY_STANDALONE" ),
                "" );
}

static CSharpCompilation CreateCompilation( string assemblyOrModuleName, SyntaxTree [] trees,
                                                CSharpCompilationOptions compilerOptions = null,
                                                IEnumerable<MetadataReference> references = null) {

    List<MetadataReference> allReferences = new List<MetadataReference>( Roslyn.domainReferences );
    if ( references != null ) {
        allReferences.AddRange( references );
    }

    CSharpCompilation compilation = CSharpCompilation.Create( assemblyOrModuleName,
                                                                trees,
                                                                options: compilerOptions,
                                                                references: allReferences );
    return compilation;
}

static void RoslynTickOverride_kmd( string [] _, bool [] context ) {
    context[0] = _playerAssembly != null;
    _roslynTick();
}


}


}
