using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class SelectionPolishValidation
{
    private const string Key = "SelectionPolishValidation.Running";
    static SelectionPolishValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void RunBatch()
    {
        SelectionPolishSetup.Install();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
    }
    private static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
        SessionState.SetBool(Key,false);
        ShaderSceneProbe.StartProbe(false,SelectionPolishSetup.Output + "/Integration",false,false,false,true);
    }
}
