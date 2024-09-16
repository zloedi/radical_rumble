using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

using UnityEngine;

namespace RR {

public class Bootstrap {

    ScriptOptions _options;

    [RuntimeInitializeOnLoadMethod]
    async Task Start() {
        _options = ScriptOptions.Default;
        _options = _options.AddReferences( Assembly.LoadFrom( ".\\RadicalRumble_Data\\Managed\\.game.dll" ) );
        string code;
        code = @"
            Qonsole.Init();
            Qonsole.Start();
            return Qonsole.Started;
        "; 
        await CSharpScript.EvaluateAsync( code, _options );
    }
}

}
