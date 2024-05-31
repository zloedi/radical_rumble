using UnityEditor;

[InitializeOnLoad]
public static class QonsoleEditorSetup {
    static QonsoleEditorSetup() {
        void duringSceneGui( SceneView sv ) {
            QonsoleBootstrap.TrySetupQonsole();
            Qonsole.OnEditorSceneGUI( sv.camera, EditorApplication.isPaused,
                                            EditorGUIUtility.pixelsPerPoint,
                                            onRepaint: Qonsole.onEditorRepaint_f );
        }
        SceneView.duringSceneGui -= duringSceneGui;
        SceneView.duringSceneGui += duringSceneGui;
        Qonsole.Log( "Qonsole setup to work in the editor." );
    }
}
