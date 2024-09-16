#if false

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

using UnityEngine;

namespace RR {

public class Bootstrap : MonoBehaviour {

    static Script _compiledOnGUI;
    static Script _compiledUpdate;
    static ScriptOptions _options;

    public static Bootstrap instance;

    void OnGUI() {
        //CompiledOnGUI();
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
        Debug.Log( "Start" );
        _options = ScriptOptions.Default;
        _options = _options.AddReferences( Assembly.LoadFrom( $"{Application.dataPath}/.Assembly/.game.dll" ) );
        string code = @"
            Qonsole.OnGUI();
        "; 
        _compiledOnGUI = CSharpScript.Create( code, _options );
        Setup();
    }

    static async Task Setup() {
        Debug.Log( "Starting..." );
        string code;
        code = @"
            int test = 0;
            var count = test + 15;
            count++;
            return count;
        "; 
        Debug.Log( code );
        try {
        var result = await CSharpScript.EvaluateAsync( code, _options );
        } catch( Exception e ) {
            Debug.LogError( e );
        }
    }

    static async Task CompiledOnGUI() {
        //if ( _compiledOnGUI == null ) {
        //    Debug.Log( "Try to compile" );
        //    string code = @"
        //        using UnityEngine;
        //        Debug.Log( ""On gui"" );
        //        Qonsole.OnGUI();
        //    "; 
        //    _compiledOnGUI = CSharpScript.Create( code, _options );
        //    Debug.Log( _compiledOnGUI );
        //}
        await _compiledOnGUI.RunAsync();
    }
}

}
#endif
