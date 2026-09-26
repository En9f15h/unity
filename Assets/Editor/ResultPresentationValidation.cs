using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ResultPresentationValidation
{
    private const string Key="ResultPresentationValidation", DefaultOutput="CodexLogs/ResultPolish-20260918";
    private static string Output
    {
        get { var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"-result-output"); return Path.GetFullPath(i>=0 && i+1<args.Length?args[i+1]:DefaultOutput); }
    }
    static ResultPresentationValidation()
    {
        EditorApplication.playModeStateChanged += state=>{
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
            {SessionState.SetBool(Key,false);ShaderSceneProbe.StartProbe(false,Output+"/Editor",captureResultPreviews:true);}
        };
    }
    public static void RunBatch()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    public static void BuildProbe()
    {
        const string bootstrap="Assets/Editor/ResultProbeBootstrap.unity";
        int code=1;
        try
        {
            Directory.CreateDirectory(Output+"/Player");
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single),bootstrap);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{bootstrap}.Concat(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path)).ToArray(),
                locationPathName=Output+"/Player/ResultProbe.exe",target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.None,extraScriptingDefines=new[]{"CODEX_SHADER_BENCHMARK"}});
            File.WriteAllText(Output+"/build-result.txt",report.summary.result+" "+report.summary.totalTime);
            code=report.summary.result==BuildResult.Succeeded?0:1;
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            AssetDatabase.DeleteAsset(bootstrap);AssetDatabase.SaveAssets();EditorApplication.Exit(code);
        }
    }
    public static void BuildGame()
    {
        Directory.CreateDirectory(Output+"/Game");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            locationPathName=Output+"/Game/IPredictedYouPredictedMe.exe",target=BuildTarget.StandaloneWindows64,
            options=BuildOptions.None});
        File.WriteAllText(Output+"/game-build-result.txt",report.summary.result+" "+report.summary.totalTime);
        EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);
    }
}

