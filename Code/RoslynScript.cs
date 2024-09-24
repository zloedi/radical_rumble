#if true

ScriptA.zloedixxx.print();
ScriptB.yy.print();

#else


using UnityEngine;
using RR;

float time = Client.clock / 1000f;
var sz = 40;
var c = Color.magenta * 0.25f; c.a = 0.98f;
var wbox = new WrapBox( 0, 0, Screen.width, Screen.height );
wbox = wbox.Center( sz * 10, sz, y: Mathf.Sin( time ) * sz );
WBUI.FillRect( wbox, color: c ); 
WBUI.Text( "This is some text widget wider.", wbox, fontSize: ( int )sz / 2, font: GUIUnity.font,
                                                                align: TextAnchor.MiddleCenter );

//QGL.LatePrint( Client.mousePosScreen, Client.mousePosScreen, scale: 2 );

static GameObject model;
static int animSource;
static Animo.Crossfade crossfade1 = new Animo.Crossfade();
static Animo.Crossfade crossfade2 = new Animo.Crossfade();
if ( model == null ) {
    model = UnityLoad( $"mdl_{Cellophane.NormalizeName( "Zombie" )}" ) as GameObject;
    animSource = Animo.RegisterAnimationSource( model );
}

ImObject imo = IMGO.RegisterPrefab( model, garbageMaterials: true, caller: model.name );
imo.go.transform.position = new Vector3( 22, 0, 20 );
imo.go.transform.localScale = Vector3.one * 4;
imo.go.transform.eulerAngles = new Vector3( 0, time * 10, 0 );
Animo.UpdateState( Client.clockDelta, animSource, crossfade1, state: 1, speed: 1 );
Animo.SampleAnimations( animSource, imo.go.GetComponent<Animator>(), crossfade1 );

imo = IMGO.RegisterPrefab( model, garbageMaterials: true, caller: model.name );
var t = imo.go.transform;
t.position = new Vector3( 26, 0, 20 );
t.localScale = Vector3.one * 2.5f;
t.eulerAngles = new Vector3( 0, time * 20, 0 );
Animo.UpdateState( Client.clockDelta, animSource, crossfade2, state: 3, speed: 1 );
Animo.SampleAnimations( animSource, imo.go.GetComponent<Animator>(), crossfade2 );

static UnityEngine.Object UnityLoad( string name ) {
    UnityEngine.Object result = Resources.Load( name );
    if ( ! result ) {
        Client.Error( $"Failed to load '{name}'" );
        return null;
    }
    Client.Log( $"Loaded '{name}'" );
    return result;
}

//Camera.main.transform.eulerAngles += new Vector3( 0, 10 * Time.deltaTime, 0 );
#endif
