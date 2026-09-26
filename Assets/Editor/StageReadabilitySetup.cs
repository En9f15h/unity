using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StageReadabilitySetup
{
    public const string Output = "CodexLogs/StageReadability-20260916";
    [MenuItem("Tools/Combat Shaders/Install Stage Readability")]
    public static void Install()
    {
        Directory.CreateDirectory(Output);
        Material background = Make("StageBackground", "Combat/Stage Background");
        Make("StageBackgroundUI", "Combat/Stage Background UI");
        const string path = "Assets/Resources/Combat/StageReadabilityPalette.asset";
        var palette = AssetDatabase.LoadAssetAtPath<StageReadabilityPalette>(path);
        if (palette == null) { palette = ScriptableObject.CreateInstance<StageReadabilityPalette>(); AssetDatabase.CreateAsset(palette, path); }
        string folder = "Assets/BattleSceneImage/VersionNew/";
        palette.entries = new[] {
            Entry(folder+"arena_1920x1088.png", .88f, .93f, .88f, new Color(.58f,.7f,.84f), 1.35f),
            Entry(folder+"castle_1920x1088.png", .72f, .90f, .78f, new Color(.46f,.66f,.86f), 1.3f),
            Entry(folder+"ChatGPT Image 2026年9月13日 下午05_10_44.png", 1f, .992f, .95f, new Color(.64f,.73f,.86f), 1.5f)
        };
        if (palette.entries.Any(e => e.background == null)) throw new InvalidOperationException("Missing readability background");
        EditorUtility.SetDirty(palette);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        var sync = UnityEngine.Object.FindFirstObjectByType<BackgroundSpriteSync>();
        var renderer = (SpriteRenderer)new SerializedObject(sync).FindProperty("backgroundRenderer").objectReferenceValue;
        var controller = renderer.GetComponent<StageReadabilityController>() ?? renderer.gameObject.AddComponent<StageReadabilityController>();
        var data = new SerializedObject(controller);
        data.FindProperty("backgroundRenderer").objectReferenceValue = renderer;
        data.FindProperty("palette").objectReferenceValue = palette;
        data.FindProperty("backgroundMaterial").objectReferenceValue = background;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("STAGE_READABILITY_SETUP PASSED");
    }
    private static StageReadabilityPalette.Entry Entry(string path, float brightness, float contrast, float saturation, Color edge, float multiplier)
    {
        return new StageReadabilityPalette.Entry { background = AssetDatabase.LoadAssetAtPath<Sprite>(path), brightness = brightness,
            contrast = contrast, saturation = saturation, edgeColor = edge, edgeColorMix = .35f, edgeMultiplier = multiplier };
    }
    private static Material Make(string name, string shaderName)
    {
        var shader = Shader.Find(shaderName);
        if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException(shaderName);
        string path = "Assets/Resources/Combat/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; EditorUtility.SetDirty(material); return material;
    }
}
