using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ShaderSceneIntegrationValidation
{
    private const string Key="ShaderSceneIntegrationValidation.Running";
    private const string Root="CodexLogs/SceneIntegration-20260915";
    static ShaderSceneIntegrationValidation() { EditorApplication.playModeStateChanged+=OnPlay; }
    public static void RunBatch()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
    }
    private static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
        SessionState.SetBool(Key,false); ShaderSceneProbe.StartProbe(false,Root+"/Editor");
    }
    public static void BuildBenchmark()
    {
        bool timing=PlayerSettings.enableFrameTimingStats;
        string bootstrap="Assets/Editor/ShaderBenchmarkBootstrap.unity";
        string executable=Root+"/Player/ShaderBenchmark.exe";
        int code=1;
        try
        {
            Directory.CreateDirectory(Root+"/Player");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene,bootstrap);
            PlayerSettings.enableFrameTimingStats=true;
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{bootstrap,"Assets/Scenes/CharacterSelectScene.unity","Assets/Scenes/GameScene.unity"},
                locationPathName=executable,target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development,
                extraScriptingDefines=new[]{"CODEX_SHADER_BENCHMARK"}
            });
            File.WriteAllText(Root+"/build-result.txt",result.summary.result+"\n"+result.summary.totalSize+" bytes\n"+result.summary.totalTime);
            code=result.summary.result==BuildResult.Succeeded?0:1;
        }
        catch(Exception e) { Debug.LogException(e); }
        finally
        {
            PlayerSettings.enableFrameTimingStats=timing;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            AssetDatabase.DeleteAsset(bootstrap);
            AssetDatabase.SaveAssets();
        }
        Debug.Log("SHADER_BENCHMARK_BUILD "+(code==0?"PASSED":"FAILED")); EditorApplication.Exit(code);
    }
}
