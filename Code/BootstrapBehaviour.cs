#if false
namespace RR {

using UnityEngine;

class BootstrapBehaviour : MonoBehaviour {

[RuntimeInitializeOnLoadMethod]
static void Bootstrap()
{
    Debug.Log( "Bootstrap" );
}

}


}
#else
using System;
using System.IO;
using System.Threading.Tasks;
//using System.ComponentModel;
//using System.IO;
using System.Reflection;

using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

#if UNITY_STANDALONE
using UnityEngine;
#else
using SDLPorts;
using GalliumMath;
#endif

namespace RR {


class BootstrapBehaviour : MonoBehaviour {

//static Action _tick;
//AppDomain _appDomain;
public static BootstrapBehaviour instance;

[RuntimeInitializeOnLoadMethod]
static void Bootstrap()
{
    var components = GameObject.FindObjectsOfType<BootstrapBehaviour>();
    if ( components.Length == 0 ) {
        GameObject go = new GameObject( "BootstrapBehaviour" );
        GameObject.DontDestroyOnLoad( go );
        instance = go.AddComponent<BootstrapBehaviour>();
        Debug.Log( "Created BootstrapBehaviour: " + instance );
    } else {
        Debug.Log( "Already have BootstrapBehaviour" );
    }
}

#if false
static void HotReload_kmd( string [] _ )
{
    Debug.Log( "Hot reloading the assembly..." );

    try {

#if false
        AppDomain.Unload( AppDomain.CurrentDomain );
#if false
    if ( _appDomain != null ) {
        Debug.Log( "Unloading first." );
        AppDomain.Unload( _appDomain );
        _appDomain = null;
    }

    _appDomain = AppDomain.CreateDomain( "ZloediAppDomain " + Guid.NewGuid() );
    string path = ".\\RadicalRumble_Data\\Managed\\.game.dll";
    ////string path = $"{Application.dataPath}/.game.dll";
    //string path = $".\\Assets\\.game.dll";

    Debug.Log( path );
    Debug.Log( _appDomain.BaseDirectory );

    ////var bytes = File.ReadAllBytes( path );

    ////AssemblyName assemblyName = new AssemblyName();
    ////assemblyName.CodeBase = path;
    //////Assembly assembly = _appDomain.Load( assemblyName );
    ////Assembly assembly = _appDomain.Load( bytes );

    var assemblyName = AssemblyName.GetAssemblyName( path );
    Debug.Log( assemblyName );
    Debug.Log( assemblyName.Name );

    var obj = _appDomain.CreateInstanceAndUnwrap( assemblyName.Name, "ZloediRemote" );
    Debug.Log( $"Loaded {path} domain: {_appDomain.GetHashCode()}" );//obj: {obj}" );
#endif

    string path = $"{Application.dataPath}/Managed/.game.dll";
    Assembly assembly = Assembly.LoadFrom( path );
    var obj = assembly.CreateInstance( "ZloediRemote" );

    BindingFlags bfs = BindingFlags.Public | BindingFlags.Instance;
    Debug.Log( bfs );
    MethodInfo [] methods = obj.GetType().GetMethods( bfs );
    Debug.Log( obj.GetType() );
    Debug.Log( methods.Length );
    //Debug.Log( typeof( obj ) );
    foreach ( MethodInfo mi in methods ) {
        Debug.Log( mi.Name );
        if ( mi.Name == "Tick" ) {
            _tick = mi.CreateDelegate( typeof( Action ), obj ) as Action;
            Debug.Log( "Tick got set" );
            break;
        }
    }

    Debug.Log( $"Loaded {path} obj: {obj}" );
#endif

    } catch ( Exception e ) {
        Debug.LogError( e );
    }
}
#endif

Script _compiledOnGUI;
Script _compiledUpdate;
ScriptOptions _options;

void Awake() {
    _options = ScriptOptions.Default;
    _options = _options.AddReferences( Assembly.LoadFrom( ".\\RadicalRumble_Data\\Managed\\.game.dll" ) );
    CompiledInit();
    WatchSetup();
}

async Task CompiledTick() {
    string code = @"
        Qonsole.Update();
    "; 
    await CSharpScript.EvaluateAsync( code, _options );
}

async Task CompiledUpdate() {
    if ( _compiledUpdate == null ) {
        string code = @"
            Qonsole.Update();
        "; 
        _compiledUpdate = CSharpScript.Create( code, _options );
    }
    await _compiledUpdate.RunAsync();
}

async Task CompiledOnGUI() {
    if ( _compiledOnGUI == null ) {
        string code = @"
            Qonsole.OnGUI();
        "; 
        _compiledOnGUI = CSharpScript.Create( code, _options );
    }
    await _compiledOnGUI.RunAsync();
}

async Task CompiledInit() {
    string code;
    code = @"
        Qonsole.Init();
        Qonsole.Start();
        return Qonsole.Started;
    "; 
    await CSharpScript.EvaluateAsync( code, _options );
}

Script _roslyn;
async Task RoslynScript() {
    if ( _roslyn == null ) {
        return;
    }

    try {
    await _roslyn.RunAsync();
    } catch ( Exception e ) {
        Debug.LogError( e );
    }
}

static void RoslynScript_kmd( string [] _ ) {
    instance.RoslynScript();
}

void Update() {
    CompiledUpdate();
}

void OnGUI() {
    CompiledOnGUI();
}

void WatchSetup() {
    var watcher = new FileSystemWatcher(@"c:\cygwin64\tmp\roslyn");

    watcher.NotifyFilter = NotifyFilters.Attributes
                            | NotifyFilters.CreationTime
                            | NotifyFilters.DirectoryName
                            | NotifyFilters.FileName
                            | NotifyFilters.LastAccess
                            | NotifyFilters.LastWrite
                            | NotifyFilters.Security
                            | NotifyFilters.Size;

    watcher.Changed += OnChanged;
    //watcher.Created += OnCreated;
    //watcher.Deleted += OnDeleted;
    //watcher.Renamed += OnRenamed;
    //watcher.Error += OnError;

    watcher.Filter = "*.cs";
    watcher.IncludeSubdirectories = true;
    watcher.EnableRaisingEvents = true;
}

private void OnChanged( object sender, FileSystemEventArgs fsa ) {
    if ( fsa.ChangeType != WatcherChangeTypes.Changed ) {
        return;
    }

    try {
        string code = File.ReadAllText( fsa.FullPath );
        _roslyn = CSharpScript.Create( code, _options );
    } catch ( Exception e ) {
        Debug.LogError( e );
    }
}

} // Main


} // RR
#endif
