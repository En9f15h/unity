using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Explicit installation only; never runs on import or when entering Play mode.
public static class CombatShaderGraphIntegration
{
    const string Root = "Assets/Resources/Combat/Materials/";
    public static readonly string[] FlowPrefabs = {
        "Oracle_RiftWarningVFX", "Oracle_ShiftVFX", "OracleShiftEnterFX", "Oracle_SightVFX"
    };
    public static readonly string[] DissolvePrefabs = { "Oracle_FadeVFX", "OracleShiftExitFX" };
    public static void InstallBatch()
    {
        try
        {
            var flow = CreateMaterial("OracleGraphFlow", "EnergyFlow", new Color(1.05f, 1.02f, 1.12f, 1));
            flow.SetFloat("_Intensity", 1.25f); flow.SetFloat("_Opacity", 1);
            flow.SetFloat("_FlowStrength", .3f); flow.SetFloat("_FlowSpeed", .35f);
            var dissolve = CreateMaterial("OracleGraphDissolve", "EdgeDissolve", new Color(.9f, 1.05f, 1.15f, 1));
            dissolve.SetColor("_EdgeColor", new Color(.3f, .65f, 1.4f, 1));
            dissolve.SetFloat("_EdgeIntensity", .8f); dissolve.SetFloat("_EdgeWidth", .045f);
            dissolve.SetFloat("_Softness", .04f);
            foreach (var name in FlowPrefabs) Bind(name, flow, 1);
            foreach (var name in DissolvePrefabs) Bind(name, dissolve, name == "Oracle_FadeVFX" ? .5f : 1);
            EditorUtility.SetDirty(flow); EditorUtility.SetDirty(dissolve); AssetDatabase.SaveAssets();
            Debug.Log("GRAPH_INTEGRATION_INSTALLED: 6 production prefabs, 2 materials");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static Material CreateMaterial(string name, string graph, Color tint)
    {
        string path = Root + name + ".mat";
        if (File.Exists(path)) throw new InvalidOperationException("Already installed; edit the existing material: " + path);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ShaderGraphWorkshop/" + graph + ".shadergraph");
        if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader)) throw new Exception("Graph did not compile: " + graph);
        var material = new Material(shader) { name = name }; material.SetColor("_Tint", tint);
        AssetDatabase.CreateAsset(material, path); return material;
    }
    static void Bind(string name, Material material, float end)
    {
        string path = "Assets/Resources/Prefab/Oracle/vfx/" + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
            if (sprites.Length == 0) throw new Exception("No sprites in " + path);
            foreach (var sprite in sprites) sprite.sharedMaterial = material;
            var driver = root.GetComponent<CombatEffectShaderDriver>();
            if (driver == null) throw new Exception("Missing duration driver in " + path);
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("graphDissolveEnd").floatValue = end;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
