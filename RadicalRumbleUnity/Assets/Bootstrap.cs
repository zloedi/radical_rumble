#if true


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

public class Bootstrap : MonoBehaviour {

    static Script _compiledSetup;
    static Script _compiledOnGUI;
    static Script _compiledUpdate;
    static ScriptOptions _options;

    public static Bootstrap instance;

    void OnGUI() {
        CompiledOnGUI().Wait();
    }

    void Update() {
        CompiledUpdate().Wait();
    }

    [RuntimeInitializeOnLoadMethod]
    static void StartStatic() {
        var components = GameObject.FindObjectsOfType<Bootstrap>();
        if ( components.Length == 0 ) {
            GameObject go = new GameObject( "Bootstrap" );
            GameObject.DontDestroyOnLoad( go );
            instance = go.AddComponent<Bootstrap>();
            Debug.Log( "Created Bootstrap: " + instance );
        } else {
            Debug.Log( "Already have Bootstrap" );
        }

        Debug.Log( "Start " + AppDomain.CurrentDomain.BaseDirectory );
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach ( var a in assemblies ) {
            Debug.Log( a.Location );
        }

        _options = ScriptOptions.Default;
        _options = _options.AddReferences( Assembly.LoadFrom( $"{Application.dataPath}/.game.dll" ) );

        string code;
        
        code = @"
            public class some {
                public some() {
                    var p = new RR.Pawn();
                    Qonsole.Log( p.filter.no_garbage.Count );
                }
            }
            new some();
            Qonsole.Init();
            Qonsole.Start();
        "; 

        try {
            _compiledSetup = CSharpScript.Create( code, _options );
        } catch( Exception e ) {
            Debug.LogError( e );
        }

        code = @"Qonsole.OnGUI();"; 
        _compiledOnGUI = CSharpScript.Create( code, _options );

                
        code = @"
            Qonsole.Update();
        "; 
        _compiledUpdate = CSharpScript.Create( code, _options );

        Setup().Wait();
    }

    static async Task Setup() {
        Debug.Log( "Starting...." );
        //string code;
        //code = @"
        //    int test = 0;
        //    var count = test + 15;
        //    count++;
        //    return count;
        //"; 
        try {
            await _compiledSetup.RunAsync();
        } catch( Exception e ) {
            Debug.LogError( e );
        }
        Debug.Log( "...done" );
    }

    static async Task CompiledOnGUI() {
        await _compiledOnGUI.RunAsync();
    }

    static async Task CompiledUpdate() {
        await _compiledUpdate.RunAsync();
    }

    static void RoslynScript_kmd( string [] _ ) {
    }
}

}
#endif
