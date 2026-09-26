using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HealthHudSetup
{
    public const string Output="CodexLogs/HealthHud-20260915";
    [MenuItem("Tools/Combat Shaders/Install Health HUD Polish")]
    public static void Install()
    {
        Directory.CreateDirectory(Output);
        var shader=Shader.Find("Combat/Health Gauge");
        if(shader==null) throw new InvalidOperationException("Health Gauge shader missing");
        const string path="Assets/Resources/Combat/Materials/HealthGauge.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
        material.shader=shader; EditorUtility.SetDirty(material);
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        var bars=UnityEngine.Object.FindObjectsByType<DirectionalHealthBarUI>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        if(bars.Length!=2) throw new InvalidOperationException("Expected two scene health bars");
        foreach(var bar in bars)
        {
            var data=new SerializedObject(bar);
            var fill=(Transform)data.FindProperty("fill").objectReferenceValue;
            if(fill==null || fill.GetComponent<SpriteRenderer>()==null) throw new InvalidOperationException("Missing sprite fill on "+bar.name);
            // Put the small, readable owner/HP label inside the gauge, freeing the action row below.
            data.FindProperty("perspectiveLabelLocalOffset").vector3Value=new Vector3(fill.localPosition.x,fill.localPosition.y,-.05f);
            data.FindProperty("perspectiveLabelCharacterSize").floatValue=.026f;
            data.FindProperty("youLabelColor").colorValue=new Color(.86f,.94f,.9f);
            data.FindProperty("enemyLabelColor").colorValue=new Color(.98f,.9f,.83f);
            data.ApplyModifiedPropertiesWithoutUndo();
            var presentation=bar.GetComponent<HealthBarPresentation>()??bar.gameObject.AddComponent<HealthBarPresentation>();
            var style=new SerializedObject(presentation); style.FindProperty("gaugeMaterial").objectReferenceValue=material; style.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        if(!shader.isSupported || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Health Gauge failed shader validation");
        Debug.Log("HEALTH_HUD_SETUP PASSED");
    }
}
