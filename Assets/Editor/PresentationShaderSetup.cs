using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PresentationShaderSetup
{
    public const string MaterialRoot="Assets/Resources/Combat/Materials/";
    private static Material gold,cyan,rune,mistUI,blood,bloodParticle;
    public static void InstallBatch()
    {
        try { Install(); Debug.Log("PRESENTATION_SHADER_SETUP PASSED"); EditorApplication.Exit(0); }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    [MenuItem("Tools/Combat Shaders/Install Second Batch")]
    public static void Install()
    {
        Directory.CreateDirectory(MaterialRoot); AssetDatabase.Refresh();
        gold=UI("UI_Gold",new Color(0.95f,0.68f,0.28f),0,0.28f);
        cyan=UI("UI_Cyan",new Color(0.25f,0.75f,1f),1,0.2f);
        rune=UI("UI_Rune",new Color(0.5f,0.5f,1f),2,0.24f);
        mistUI=UI("UI_Mist",new Color(0.4f,0.55f,0.7f),3,0);
        Make("StageMist","Combat/Stage Atmosphere");
        blood=Make("BloodDecal","Combat/Blood"); blood.SetFloat("_Particle",0);
        bloodParticle=Make("BloodSpray","Combat/Blood"); bloodParticle.SetFloat("_Particle",1);
        ConfigureBlood("Assets/Resources/blood/bloodDecalPrefab.prefab");
        ConfigureBlood("Assets/Resources/blood/bloodSprayPrefab.prefab");
        foreach(string path in new[]{"Assets/Resources/Prefab/knight/energyBar.prefab","Assets/Resources/Prefab/Oracle/UI/OracleEnergyBar.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try { ConfigureEnergy(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        string palettePath="Assets/Resources/Combat/StageAtmospherePalette.asset";
        var palette=AssetDatabase.LoadAssetAtPath<StageAtmospherePalette>(palettePath);
        if(palette==null) { palette=ScriptableObject.CreateInstance<StageAtmospherePalette>(); AssetDatabase.CreateAsset(palette,palettePath); }
        string folder="Assets/BattleSceneImage/VersionNew/";
        palette.entries=new[]{
            new StageAtmospherePalette.Entry{background=AssetDatabase.LoadAssetAtPath<Sprite>(folder+"arena_1920x1088.png"),color=new Color(0.5f,0.56f,0.6f),density=0.055f},
            new StageAtmospherePalette.Entry{background=AssetDatabase.LoadAssetAtPath<Sprite>(folder+"castle_1920x1088.png"),color=new Color(0.76f,0.65f,0.4f),density=0.05f},
            new StageAtmospherePalette.Entry{background=AssetDatabase.LoadAssetAtPath<Sprite>(folder+"ChatGPT Image 2026年9月13日 下午05_10_44.png"),color=new Color(0.35f,0.48f,0.62f),density=0.09f}
        };
        if(palette.entries.Any(e=>e.background==null)) throw new InvalidOperationException("Missing atmosphere background reference");
        EditorUtility.SetDirty(palette);
        var battle=EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        foreach(var root in battle.GetRootGameObjects()) ConfigureEnergy(root);
        var sync=UnityEngine.Object.FindFirstObjectByType<BackgroundSpriteSync>();
        var syncData=new SerializedObject(sync);
        var background=(SpriteRenderer)syncData.FindProperty("backgroundRenderer").objectReferenceValue;
        var controller=background.GetComponent<StageAtmosphereController>() ?? background.gameObject.AddComponent<StageAtmosphereController>();
        var controllerData=new SerializedObject(controller);
        controllerData.FindProperty("backgroundRenderer").objectReferenceValue=background;
        controllerData.FindProperty("palette").objectReferenceValue=palette;
        controllerData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(battle); EditorSceneManager.SaveScene(battle);
        var selection=EditorSceneManager.OpenScene("Assets/Scenes/CharacterSelectScene.unity");
        foreach(var panel in UnityEngine.Object.FindObjectsByType<CharacterSelectionPlayerPanel>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var data=new SerializedObject(panel);
            var button=data.FindProperty("confirmButton").objectReferenceValue as Button;
            if(button!=null) ConfigureUI(button.image,gold,false);
            var ready=data.FindProperty("readyEffectRoot").objectReferenceValue as GameObject;
            if(ready!=null) foreach(var image in ready.GetComponentsInChildren<Image>(true)) ConfigureUI(image,gold,true);
        }
        var manager=UnityEngine.Object.FindFirstObjectByType<CharacterSelectionManager>();
        var managerData=new SerializedObject(manager);
        var vs=managerData.FindProperty("vsAnimation").objectReferenceValue as CharacterSelectionAnimationController;
        if(vs!=null) foreach(var image in vs.GetComponentsInChildren<Image>(true)) ConfigureUI(image,gold,false);
        EditorSceneManager.MarkSceneDirty(selection); EditorSceneManager.SaveScene(selection);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        foreach(var name in new[]{"Combat/Presentation UI","Combat/Blood","Combat/Stage Atmosphere"})
            if(ShaderUtil.ShaderHasError(Shader.Find(name))) throw new InvalidOperationException("Shader error: "+name);
    }
    private static Material Make(string name,string shaderName)
    {
        var shader=Shader.Find(shaderName); if(shader==null) throw new InvalidOperationException(shaderName);
        var path=MaterialRoot+name+".mat"; var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
        material.shader=shader; material.renderQueue=3000;
        if(material.HasProperty("_Color")) material.SetColor("_Color",Color.white);
        EditorUtility.SetDirty(material); return material;
    }
    private static Material UI(string name,Color color,float mode,float strength)
    {
        var material=Make(name,"Combat/Presentation UI");
        material.SetColor("_AccentColor",color); material.SetFloat("_Mode",mode); material.SetFloat("_Strength",strength); return material;
    }
    private static void ConfigureUI(Image image,Material style,bool pulse)
    {
        if(image==null) return;
        var component=image.GetComponent<UIShaderFeedback>() ?? image.gameObject.AddComponent<UIShaderFeedback>();
        var data=new SerializedObject(component);
        data.FindProperty("styleMaterial").objectReferenceValue=style; data.FindProperty("pulseOnEnable").boolValue=pulse;
        data.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("Presentation UI: "+image.name+" / "+style.name);
    }
    private static void ConfigureEnergy(GameObject root)
    {
        foreach(var energy in root.GetComponentsInChildren<EnergyBarUI>(true))
        {
            var data=new SerializedObject(energy);
            ConfigureUI(data.FindProperty("fillImage").objectReferenceValue as Image,cyan,false);
            var eye=data.FindProperty("eyeImage");
            if(eye!=null) ConfigureUI(eye.objectReferenceValue as Image,rune,false);
        }
    }
    private static void ConfigureBlood(string path)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach(var sprite in root.GetComponentsInChildren<SpriteRenderer>(true)) sprite.sharedMaterial=blood;
            foreach(var particles in root.GetComponentsInChildren<ParticleSystemRenderer>(true)) particles.sharedMaterial=bloodParticle;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
