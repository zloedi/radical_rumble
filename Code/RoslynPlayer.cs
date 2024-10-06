using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using UnityEngine;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RR {


public static class RoslynPlayer {


public static Action<object> Log = o => {};
public static Action<object> Error = o => {};

static List<MetadataReference> _domainReferences = new List<MetadataReference>();

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

static bool _initialized;
public static void Init() {
    if ( _initialized ) {
        return;
    }

    _sourcesDir = $"{Application.dataPath}/../../Code/";

    var watcher = new FileSystemWatcher( _sourcesDir );
    watcher.Filter = "*.cs";
    watcher.NotifyFilter = NotifyFilters.LastWrite;

    watcher.Changed += OnScriptChanged;
    watcher.Error += OnScriptError;

    watcher.IncludeSubdirectories = false;
    watcher.EnableRaisingEvents = true;

    Log( $"Setup File Watcher to {_sourcesDir}" );

    _initialized = true;
}

// make sure we are detach from the Unity editor on play mode off.
public static void Done() {
    _domainReferences.Clear();
}

// will cause the game assembly dll to be grabbed by Unity,
// won't be able to replace with a new version.
static bool _initializedCompiler;
static void TryInitCompiler() {
    if ( _initializedCompiler )
        return;

    var domainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
    foreach ( var a in domainAssemblies ) {
        MetadataReference reference = AssemblyMetadata
                                        .CreateFromFile( a.Location )
                                        .GetReference();
        _domainReferences.Add( reference );
    }
    _initializedCompiler = true;
}

static void OnScriptChanged( object sender, FileSystemEventArgs e ) {
    if ( e.ChangeType != WatcherChangeTypes.Changed ) {
        return;
    }

    if ( Array.IndexOf( _scriptSources, e.Name ) < 0 )
        return;

    TryInitCompiler();

    Log( $"Script changed: {e.FullPath}, recompiling..." );

    var trees = new SyntaxTree[_scriptSources.Length];

    for ( int i = 0; i < _scriptSources.Length; i++ ) {
        string path = _sourcesDir + _scriptSources[i];
        if ( ! ParseFile( path, true, out trees[i] ) ) {
            return;
        }
        Log( $"Parsed {path}" );
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
        Error( ex );
    }
}

static void OnScriptError( object sender, ErrorEventArgs e ) {
    Error( e.GetException() );
}

static bool ParseFile( string path, bool dll, out SyntaxTree tree ) {
    try {
        tree = Parse( File.ReadAllText( path ) );
        return true;
    } catch ( Exception ex ) {
        tree = null;
        Error( ex );
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
                Error( "Compilation failed." );
                foreach ( var d in result.Diagnostics ) {
                    Error( d );
                }
                return false;
            }
            ms.Seek( 0, SeekOrigin.Begin );
            image = ms.ToArray();
        }

        return true;

    } catch ( Exception ex ) {
        Error( ex );
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

    List<MetadataReference> allReferences = new List<MetadataReference>( _domainReferences );
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
