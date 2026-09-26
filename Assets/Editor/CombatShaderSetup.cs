using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class CombatShaderSetup
{
    public const string MaterialRoot = "Assets/Resources/Combat/Materials/";
    public static readonly string[] CharacterPaths = {
        "Assets/Resources/Prefab/knight/knight_0.prefab", "Assets/Resources/Prefab/knight/knight_1.prefab",
        "Assets/Resources/Prefab/Oracle/Oracle_0.prefab", "Assets/Resources/Prefab/Oracle/Oracle_1.prefab"
    };
    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

    [MenuItem("Tools/Combat Shaders/Install First Batch")]
    public static void Install()
    {
        Directory.CreateDirectory(MaterialRoot);
        AssetDatabase.Refresh();
        CreateMaterials();
        foreach (string path in CharacterPaths) ConfigureCharacter(path);
        ConfigureUIPreviews();
        foreach (string path in Directory.GetFiles("Assets/Resources/Prefab/Oracle/vfx", "*.prefab")) ConfigureEffect(path.Replace('\\','/'));
        foreach (string name in new[] { "LightningStrikePrefab", "SwordResidualLightningPrefab", "ParrySuccessEffect", "Ultimate" })
            ConfigureEffect("Assets/Resources/Prefab/knight/" + name + ".prefab");
        ConfigureLegacyMaterials();
        ConfigureBattleBloom();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log("COMBAT_SHADER_SETUP PASSED");
    }

    public static void InstallBatch()
    {
        try { Install(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static Material Make(string name, string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Missing shader " + shaderName);
        string path = MaterialRoot + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetColor("_Color", Color.white);
        material.enableInstancing = true;
        materials[name] = material;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material Energy(string name, Color glow, float intensity, float mode, float additive)
    {
        Material m = Make(name, "Combat/Energy Sprite");
        m.SetColor("_GlowColor", glow); m.SetFloat("_Intensity", intensity); m.SetFloat("_Mode", mode);
        m.SetFloat("_Additive", additive); m.SetFloat("_GlowStrength", 0.32f);
        m.SetFloat("_FlowStrength", 0.14f); m.SetFloat("_FlowSpeed", mode == 2 ? 0.7f : 1f);
        return m;
    }

    private static Material Ribbon(string name, Color core, Color edge, bool particle)
    {
        Material m = Make(name, "Combat/Energy Ribbon and Particle");
        m.SetColor("_CoreColor",core); m.SetColor("_EdgeColor",edge);
        m.SetFloat("_Particle",particle ? 1 : 0); m.SetFloat("_Intensity",1.1f);
        m.SetFloat("_Additive",0.7f); m.SetFloat("_FlowSpeed",particle ? 0.8f : 2.5f);
        return m;
    }

    private static void CreateMaterials()
    {
        materials.Clear();
        foreach (string name in new[] { "CharacterKnight", "CharacterOracle", "CharacterSketch" })
        {
            Material m = Make(name,"Combat/Character Lit");
            m.SetFloat("_RimStrength",name == "CharacterSketch" ? 0.025f : 0.08f);
            m.SetColor("_RimColor",name == "CharacterKnight" ? new Color(0.85f,0.7f,0.4f) : new Color(0.3f,0.6f,0.9f));
            m.SetColor("_PhaseColor",new Color(0.4f,1.4f,2f));
            m.SetFloat("_Dissolve",0); m.SetFloat("_HitAmount",0);
        }
        Energy("OracleEnergy",new Color(0.3f,0.8f,1.2f),1.15f,0,0.5f);
        Energy("OracleBurst",new Color(0.65f,0.35f,1.4f),1.35f,0,0.6f);
        Energy("OracleRune",new Color(0.55f,0.35f,1.3f),1.15f,2,0.4f);
        Material ward=Energy("OracleWard",new Color(0.3f,0.9f,1.5f),0.85f,1,0.2f);
        ward.SetFloat("_Opacity",0.5f); ward.SetFloat("_GlowStrength",0.16f);
        Energy("OracleGhost",new Color(0.3f,0.7f,1.2f),0.85f,3,0.25f);
        Energy("KnightImpact",new Color(1.5f,0.95f,0.3f),1.25f,0,0.55f);
        Ribbon("KnightLightning",new Color(2f,2.3f,2.8f),new Color(0.15f,0.45f,1.3f),false);
        Ribbon("KnightSlash",new Color(1.8f,1.5f,0.9f),new Color(1f,0.5f,0.15f),false);
        Ribbon("OracleParticles",Color.white,Color.white,true);
        Ribbon("KnightParticles",new Color(1.4f,1.25f,1f),new Color(0.6f,0.75f,1f),true);
    }

    private static bool IsBody(SpriteRenderer renderer)
    {
        if (renderer.sprite == null) return false;
        string source = AssetDatabase.GetAssetPath(renderer.sprite);
        return source.EndsWith(".psd",StringComparison.OrdinalIgnoreCase) || source.EndsWith("/knight.png") || source.EndsWith("/Oracle.png");
    }

    private static void ConfigureUIPreviews()
    {
        foreach(string path in Directory.GetFiles("Assets/Generated/CharacterSelection/Prefabs/UICharacters","*.prefab"))
        {
            GameObject root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(UnityEngine.UI.Image image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    if(image.sprite==null) continue;
                    Material material=Make(Path.GetFileNameWithoutExtension(path),"Combat/Character UI");
                    material.SetVector("_SpriteUVRect",UnityEngine.Sprites.DataUtility.GetOuterUV(image.sprite));
                    material.SetFloat("_RimStrength",path.Contains("_0_") ? 0.025f : 0.06f);
                    material.SetColor("_RimColor",path.Contains("knight") ? new Color(0.8f,0.65f,0.35f) : new Color(0.3f,0.6f,0.9f));
                    image.material=material;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    private static void ConfigureCharacter(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            SpriteRenderer[] body = root.GetComponentsInChildren<SpriteRenderer>(true).Where(IsBody).ToArray();
            if (body.Length == 0) throw new InvalidOperationException("No body sprites in " + path);
            bool sketch = body.All(r => !AssetDatabase.GetAssetPath(r.sprite).EndsWith(".psd"));
            bool oracle = path.IndexOf("oracle",StringComparison.OrdinalIgnoreCase) >= 0;
            Material material = materials[sketch ? "CharacterSketch" : oracle ? "CharacterOracle" : "CharacterKnight"];
            CharacterUnit unit = root.GetComponentInChildren<CharacterUnit>(true);
            GameObject owner = unit != null ? unit.gameObject : root;
            CharacterShaderFeedback feedback = owner.GetComponent<CharacterShaderFeedback>() ?? owner.AddComponent<CharacterShaderFeedback>();
            SerializedObject serialized = new SerializedObject(feedback);
            serialized.FindProperty("characterMaterial").objectReferenceValue = material;
            SerializedProperty array = serialized.FindProperty("bodyRenderers");
            array.arraySize = body.Length;
            for (int i = 0; i < body.Length; i++)
            {
                body[i].sharedMaterial = material;
                array.GetArrayElementAtIndex(i).objectReferenceValue = body[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // The character contains inactive lightning templates as well as external VFX prefabs.
            foreach (LineRenderer line in root.GetComponentsInChildren<LineRenderer>(true))
                line.sharedMaterial = materials["KnightLightning"];
            foreach (ParticleSystemRenderer particle in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                particle.sharedMaterial = materials[oracle ? "OracleParticles" : "KnightParticles"];
            OracleVFXController oracleVFX = root.GetComponentInChildren<OracleVFXController>(true);
            if (oracleVFX != null)
            {
                SerializedObject vfx = new SerializedObject(oracleVFX);
                vfx.FindProperty("additiveSpriteMaterial").objectReferenceValue = materials["OracleEnergy"];
                vfx.ApplyModifiedPropertiesWithoutUndo();
            }
            KnightUltimateVFX ultimate = root.GetComponentInChildren<KnightUltimateVFX>(true);
            if (!oracle && unit != null && ultimate != null)
            {
                SerializedObject source = new SerializedObject(ultimate);
                KnightSlashShaderVFX slash = owner.GetComponent<KnightSlashShaderVFX>() ?? owner.AddComponent<KnightSlashShaderVFX>();
                SerializedObject target = new SerializedObject(slash);
                target.FindProperty("swordTip").objectReferenceValue = source.FindProperty("swordLightningEndPoint").objectReferenceValue;
                target.FindProperty("swordBase").objectReferenceValue = source.FindProperty("swordLightningStartPoint").objectReferenceValue;
                target.FindProperty("slashMaterial").objectReferenceValue = materials["KnightSlash"];
                target.FindProperty("trailWidth").floatValue = sketch ? 0.035f : 0.07f;
                target.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
            Debug.Log("Combat character: " + path + " body sprites=" + body.Length);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureEffect(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            string name = Path.GetFileNameWithoutExtension(path);
            bool oracle = path.Contains("/Oracle/");
            string preset = "KnightImpact";
            if (oracle)
                preset = name.Contains("Ward") ? "OracleWard" : name.Contains("Warning") || name.Contains("Shift") || name.Contains("Sight") ? "OracleRune" : name.Contains("Burst") || name.Contains("Hit") ? "OracleBurst" : "OracleEnergy";
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (!IsBody(renderer)) renderer.sharedMaterial = materials[preset];
            foreach (LineRenderer renderer in root.GetComponentsInChildren<LineRenderer>(true)) renderer.sharedMaterial = materials["KnightLightning"];
            foreach (ParticleSystemRenderer renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                renderer.sharedMaterial = materials[oracle ? "OracleParticles" : "KnightParticles"];
            if (root.GetComponent<CombatEffectShaderDriver>() == null) root.AddComponent<CombatEffectShaderDriver>();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureLegacyMaterials()
    {
        foreach (string name in new[] { "M_ReadyAura", "M_RevealBurst", "M_PurpleDust", "M_LightningBurst" })
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/CharacterSelection/Materials/"+name+".mat");
            Color tint = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            m.shader = materials["OracleParticles"].shader;
            m.CopyPropertiesFromMaterial(materials["OracleParticles"]);
            m.SetColor("_Color",tint);
            if (name == "M_PurpleDust") { m.SetFloat("_Additive",0.15f); m.SetFloat("_Intensity",0.6f); }
            m.SetOverrideTag("RenderType","Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
        }
    }

    private static void ConfigureBattleBloom()
    {
        string path = "Assets/Settings/CombatBloom.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,path); }
        if (!profile.TryGet(out Bloom bloom)) { bloom = profile.Add<Bloom>(true); AssetDatabase.AddObjectToAsset(bloom,profile); }
        bloom.threshold.Override(1.15f); bloom.intensity.Override(0.16f); bloom.scatter.Override(0.45f); bloom.clamp.Override(4f);
        EditorUtility.SetDirty(profile); EditorUtility.SetDirty(bloom);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        Camera camera = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First(c=>c.CompareTag("MainCamera"));
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        GameObject volumeObject = GameObject.Find("Combat Presentation Volume") ?? new GameObject("Combat Presentation Volume");
        Volume volume = volumeObject.GetComponent<Volume>() ?? volumeObject.AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 10; volume.weight = 1; volume.sharedProfile = profile;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Combat Shaders/Validate Installation")]
    public static void Validate()
    {
        foreach (string shaderName in new[] { "Combat/Character Lit", "Combat/Energy Sprite", "Combat/Energy Ribbon and Particle", "Combat/Character UI" })
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Shader error: " + shaderName);
        }
        foreach (string path in CharacterPaths)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CharacterShaderFeedback feedback = root.GetComponentInChildren<CharacterShaderFeedback>(true);
            if (feedback == null || feedback.BodyRenderers == null || feedback.BodyRenderers.Length == 0) throw new InvalidOperationException("Character not bound: " + path);
            if (feedback.BodyRenderers.Any(r=>r==null || r.sharedMaterial==null || r.sharedMaterial.shader.name!="Combat/Character Lit"))
                throw new InvalidOperationException("Invalid character material: " + path);
        }
    }
}
