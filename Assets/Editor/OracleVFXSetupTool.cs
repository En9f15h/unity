using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class OracleVFXSetupTool
{
    private const string SourceFolder = "Assets/Resources/Character/Oracle/Animations";
    private const string CharacterFolder = "Assets/Resources/Character/Oracle";
    private const string ClipFolder = CharacterFolder + "/AnimationClips";
    private const string GeneratedSpriteFolder = ClipFolder + "/GeneratedSprites";
    private const string VFXControllerFolder = ClipFolder + "/VFXControllers";
    private const string VFXPrefabFolder = "Assets/Resources/Prefab/Oracle/vfx";
    private const string OraclePrefabPath = "Assets/Resources/Prefab/Oracle/Oracle_0.prefab";
    private const float DefaultPixelsPerUnit = 256f;

    private static readonly SheetSpec[] SheetSpecs =
    {
        new SheetSpec("Bolt Flying Animation.png", "Oracle_BoltProjectileFlight", 8, 16f, false),
        new SheetSpec("Bolt Impact Animation.png", "Oracle_BoltHit", 8, 16f, false),
        new SheetSpec("Rift Warning Animation.png", "Oracle_RiftWarning", 8, 12f, false),
        new SheetSpec("Rift Burst Animation.png", "Oracle_RiftBurst", 8, 16f, false),
        new SheetSpec("Shift Vanish Animation.png", "Oracle_ShiftVanish", 8, 16f, false),
        new SheetSpec("Shift Appear Animation.png", "Oracle_ShiftAppear", 8, 16f, false),
        new SheetSpec("Ward Activation Animation.png", "Oracle_WardActivation", 8, 16f, false),
        new SheetSpec("Ward Successful Block Animation.png", "Oracle_WardSuccess", 8, 16f, false),
        new SheetSpec("Fade Dissolve Animation.png", "Oracle_FadeDissolve", 8, 16f, false),
        new SheetSpec("Fade Evade Animation.png", "Oracle_FadeEvade", 8, 16f, false)
    };

    [MenuItem("Tools/Oracle/Setup Oracle VFX")]
    public static void SetupOracleVFX()
    {
        Debug.Log("[OracleVFXSetupTool] Setup started.");

        ValidateSourceAssets();
        EnsureFolders();
        ConfigureStaticSourceSprites();

        Dictionary<string, ClipBuildResult> clips = CreateAnimationClips();
        CreateVFXPrefabs(clips);
        OracleShiftAnimationBuilder.BuildShiftAnimationAssets();
        OracleAnimatorSetup.BuildAnimator();
        AssignOraclePrefabReferences();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[OracleVFXSetupTool] Setup completed.");
    }

    public static void SetupOracleVFXFromCommandLine()
    {
        try
        {
            SetupOracleVFX();
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    private static void ValidateSourceAssets()
    {
        RequireDirectory(SourceFolder);
        RequireFile(OraclePrefabPath);

        for (int i = 0; i < SheetSpecs.Length; i++)
            RequireFile(GetSourcePath(SheetSpecs[i].sourceFileName));

        string[] staticFiles =
        {
            "Bolt Projectile.png",
            "Rift Warning Circle.png",
            "Rift Ground Crack Remnant.png",
            "Shift Teleport Rune.png",
            "Ward Shield.png",
            "Fade Afterimage Set.png",
            "image-gen-21.png"
        };

        for (int i = 0; i < staticFiles.Length; i++)
            RequireFile(GetSourcePath(staticFiles[i]));
    }

    private static void EnsureFolders()
    {
        EnsureFolder(ClipFolder);
        EnsureFolder(GeneratedSpriteFolder);
        EnsureFolder(VFXControllerFolder);
        EnsureFolder("Assets/Resources/Prefab/Oracle");
        EnsureFolder(VFXPrefabFolder);
    }

    private static void ConfigureStaticSourceSprites()
    {
        string[] sourceSpriteFiles =
        {
            "Bolt Projectile.png",
            "Rift Warning Circle.png",
            "Rift Ground Crack Remnant.png",
            "Shift Teleport Rune.png",
            "Shift Afterimage.png",
            "Ward Shield.png",
            "Fade Afterimage Set.png",
            "image-gen-18.png",
            "image-gen-19.png",
            "image-gen-20.png",
            "image-gen-21.png",
            "image-gen-22.png"
        };

        for (int i = 0; i < sourceSpriteFiles.Length; i++)
            ConfigureTextureAsSprite(GetSourcePath(sourceSpriteFiles[i]), DefaultPixelsPerUnit);

        ConfigureTextureAsSprite(CharacterFolder + "/Sight.png", 100f);
    }

    private static Dictionary<string, ClipBuildResult> CreateAnimationClips()
    {
        Dictionary<string, ClipBuildResult> results = new Dictionary<string, ClipBuildResult>();

        for (int i = 0; i < SheetSpecs.Length; i++)
        {
            ClipBuildResult result = CreateFrameSpritesAndClip(SheetSpecs[i]);
            results[result.clip.name] = result;
            Debug.Log("[OracleVFXSetupTool] Generated VFX clip: " + AssetDatabase.GetAssetPath(result.clip));
        }

        return results;
    }

    private static ClipBuildResult CreateFrameSpritesAndClip(SheetSpec spec)
    {
        string sourcePath = GetSourcePath(spec.sourceFileName);
        Texture2D source = LoadTextureFromPng(sourcePath);
        int frameCount = Mathf.Max(1, spec.frameCount);
        int frameWidth = source.width / frameCount;
        int frameHeight = source.height;

        if (frameWidth <= 0 || source.width % frameCount != 0)
            throw new InvalidOperationException("Invalid frame slicing for " + spec.sourceFileName);

        string frameFolder = GeneratedSpriteFolder + "/" + spec.clipName;
        EnsureFolder(frameFolder);

        List<Sprite> sprites = new List<Sprite>();

        for (int i = 0; i < frameCount; i++)
        {
            Texture2D frame = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false);
            frame.SetPixels(source.GetPixels(i * frameWidth, 0, frameWidth, frameHeight));
            frame.Apply();

            string framePath = frameFolder + "/" + spec.clipName + "_" + i.ToString("00") + ".png";
            File.WriteAllBytes(ToFullPath(framePath), frame.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(frame);

            AssetDatabase.ImportAsset(framePath, ImportAssetOptions.ForceUpdate);
            ConfigureTextureAsSprite(framePath, DefaultPixelsPerUnit);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
            if (sprite == null)
                throw new InvalidOperationException("Generated frame did not import as Sprite: " + framePath);

            sprites.Add(sprite);
        }

        UnityEngine.Object.DestroyImmediate(source);

        string clipPath = ClipFolder + "/" + spec.clipName + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.name = spec.clipName;
        clip.frameRate = spec.frameRate;
        clip.ClearCurves();

        EditorCurveBinding binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count + 1];
        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / spec.frameRate,
                value = sprites[i]
            };
        }

        keyframes[sprites.Count] = new ObjectReferenceKeyframe
        {
            time = sprites.Count / spec.frameRate,
            value = sprites[sprites.Count - 1]
        };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        SetLoopTime(clip, spec.loop);
        EditorUtility.SetDirty(clip);

        return new ClipBuildResult(clip, sprites[0]);
    }

    private static void CreateVFXPrefabs(Dictionary<string, ClipBuildResult> clips)
    {
        CreateAnimatedPrefab(
            "Oracle_BoltVFX",
            clips["Oracle_BoltProjectileFlight"],
            null,
            new Vector3(0.9f, 0.9f, 1f),
            false,
            1f,
            true);

        CreateAnimatedPrefab(
            "Oracle_BoltHitVFX",
            clips["Oracle_BoltHit"],
            null,
            new Vector3(1.1f, 1.1f, 1f),
            true,
            0.8f,
            true);

        CreateAnimatedPrefab(
            "Oracle_RiftWarningVFX",
            clips["Oracle_RiftWarning"],
            LoadSprite(GetSourcePath("Rift Warning Circle.png")),
            new Vector3(1.2f, 1.2f, 1f),
            false,
            1f,
            false);

        CreateAnimatedPrefab(
            "Oracle_RiftBurstVFX",
            clips["Oracle_RiftBurst"],
            LoadSprite(GetSourcePath("Rift Ground Crack Remnant.png")),
            new Vector3(1.35f, 1.35f, 1f),
            true,
            0.9f,
            true);

        CreateStaticPrefab(
            "Oracle_ShiftVFX",
            LoadSprite(GetSourcePath("Shift Teleport Rune.png")),
            new Vector3(0.85f, 0.85f, 1f),
            true);

        CreateAnimatedPrefab(
            "Oracle_WardVFX",
            clips["Oracle_WardActivation"],
            LoadSprite(GetSourcePath("Ward Shield.png")),
            new Vector3(0.9f, 0.9f, 1f),
            false,
            1f,
            false);

        CreateAnimatedPrefab(
            "Oracle_WardSuccessVFX",
            clips["Oracle_WardSuccess"],
            null,
            new Vector3(0.95f, 0.95f, 1f),
            true,
            0.8f,
            true);

        CreateAnimatedPrefab(
            "Oracle_FadeVFX",
            clips["Oracle_FadeDissolve"],
            null,
            new Vector3(0.9f, 0.9f, 1f),
            true,
            0.8f,
            true);

        CreateStaticPrefab(
            "Oracle_SightVFX",
            LoadSprite(GetSourcePath("image-gen-21.png")),
            new Vector3(0.65f, 0.65f, 1f),
            true);
    }

    private static void CreateAnimatedPrefab(
        string prefabName,
        ClipBuildResult clipResult,
        Sprite backgroundSprite,
        Vector3 scale,
        bool autoDestroy,
        float autoDestroySeconds,
        bool addParticles)
    {
        string prefabPath = VFXPrefabFolder + "/" + prefabName + ".prefab";
        string controllerPath = VFXControllerFolder + "/" + prefabName + ".controller";
        AnimatorController controller = CreateSingleClipController(controllerPath, clipResult.clip);

        GameObject root = new GameObject(prefabName);
        try
        {
            root.transform.localScale = scale;

            if (backgroundSprite != null)
            {
                GameObject background = new GameObject("Background");
                background.transform.SetParent(root.transform, false);
                SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
                backgroundRenderer.sprite = backgroundSprite;
                backgroundRenderer.color = new Color(1f, 1f, 1f, 0.8f);
                backgroundRenderer.sortingOrder = -1;
            }

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = clipResult.firstSprite;

            Animator animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            if (addParticles)
                AddParticleChild(root.transform, "Particles");

            if (autoDestroy)
                AddAutoDestroy(root, autoDestroySeconds);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log("[OracleVFXSetupTool] Generated VFX prefab: " + prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateStaticPrefab(string prefabName, Sprite sprite, Vector3 scale, bool addParticles)
    {
        if (sprite == null)
            throw new InvalidOperationException("Missing static sprite for " + prefabName);

        string prefabPath = VFXPrefabFolder + "/" + prefabName + ".prefab";

        GameObject root = new GameObject(prefabName);
        try
        {
            root.transform.localScale = scale;

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;

            if (addParticles)
                AddParticleChild(root.transform, "Particles");

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log("[OracleVFXSetupTool] Generated VFX prefab: " + prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static AnimatorController CreateSingleClipController(string controllerPath, AnimationClip clip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);

        AnimatorState state = stateMachine.AddState(clip.name, new Vector3(250f, 120f, 0f));
        state.motion = clip;
        stateMachine.defaultState = state;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = states.Length - 1; i >= 0; i--)
            stateMachine.RemoveState(states[i].state);

        ChildAnimatorStateMachine[] machines = stateMachine.stateMachines;
        for (int i = machines.Length - 1; i >= 0; i--)
            stateMachine.RemoveStateMachine(machines[i].stateMachine);

        AnimatorStateTransition[] anyTransitions = stateMachine.anyStateTransitions;
        for (int i = anyTransitions.Length - 1; i >= 0; i--)
            stateMachine.RemoveAnyStateTransition(anyTransitions[i]);
    }

    private static void AddParticleChild(Transform parent, string name)
    {
        GameObject particlesObject = new GameObject(name);
        particlesObject.transform.SetParent(parent, false);

        ParticleSystem particleSystem = particlesObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.25f, 0.95f, 1f, 0.85f),
            new Color(0.75f, 0.2f, 1f, 0.75f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.18f;

        ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    private static void AddAutoDestroy(GameObject root, float lifeTime)
    {
        AutoDestroyEffect autoDestroy = root.AddComponent<AutoDestroyEffect>();
        SerializedObject serialized = new SerializedObject(autoDestroy);
        SerializedProperty property = serialized.FindProperty("lifeTime");
        if (property != null)
            property.floatValue = Mathf.Max(0.05f, lifeTime);

        property = serialized.FindProperty("useUnscaledTime");
        if (property != null)
            property.boolValue = true;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignOraclePrefabReferences()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(OraclePrefabPath);
        try
        {
            Transform visualRoot = FindOrCreateVisualRoot(root);
            Transform castPoint = EnsureChild(root.transform, "CastPoint", new Vector3(1.65f, 1.15f, 0f));
            Transform groundPoint = EnsureChild(root.transform, "GroundVFXPoint", new Vector3(0f, -1.55f, 0f));
            Transform centerPoint = EnsureChild(root.transform, "CenterVFXPoint", new Vector3(0f, 0.65f, 0f));
            Transform vfxRoot = EnsureChild(root.transform, "VFXRoot", Vector3.zero);

            OracleVFXController controller = root.GetComponent<OracleVFXController>();
            if (controller == null)
                controller = root.AddComponent<OracleVFXController>();

            SerializedObject serialized = new SerializedObject(controller);
            SetReference(serialized, "visualRoot", visualRoot);
            SetReference(serialized, "castPoint", castPoint);
            SetReference(serialized, "groundVFXPoint", groundPoint);
            SetReference(serialized, "centerVFXPoint", centerPoint);
            SetReference(serialized, "vfxRoot", vfxRoot);
            SetReference(serialized, "boltProjectilePrefab", LoadVFXPrefab("Oracle_BoltVFX"));
            SetReference(serialized, "boltHitPrefab", LoadVFXPrefab("Oracle_BoltHitVFX"));
            SetReference(serialized, "riftWarningPrefab", LoadVFXPrefab("Oracle_RiftWarningVFX"));
            SetReference(serialized, "riftBurstPrefab", LoadVFXPrefab("Oracle_RiftBurstVFX"));
            SetReference(serialized, "shiftVFXPrefab", LoadVFXPrefab("Oracle_ShiftVFX"));
            SetReference(serialized, "shiftExitFXPrefab", LoadVFXPrefab("OracleShiftExitFX"));
            SetReference(serialized, "shiftEnterFXPrefab", LoadVFXPrefab("OracleShiftEnterFX"));
            SetReference(serialized, "wardVFXPrefab", LoadVFXPrefab("Oracle_WardVFX"));
            SetReference(serialized, "wardSuccessPrefab", LoadVFXPrefab("Oracle_WardSuccessVFX"));
            SetReference(serialized, "fadeDissolvePrefab", LoadVFXPrefab("Oracle_FadeVFX"));
            SetReference(serialized, "sightActivationPrefab", LoadVFXPrefab("Oracle_SightVFX"));
            SetFloat(serialized, "shiftEnterYOffset", -0.8f);
            SetBool(serialized, "useWardPrefabOnly", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, OraclePrefabPath);
            Debug.Log("[OracleVFXSetupTool] Oracle prefab references assigned: " + OraclePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform FindOrCreateVisualRoot(GameObject root)
    {
        Transform visualRoot = root.transform.Find("visualRoot");
        if (visualRoot == null)
            visualRoot = root.transform.Find("VisualRoot");

        if (visualRoot != null)
            return visualRoot;

        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator != null)
            return animator.transform;

        return EnsureChild(root.transform, "visualRoot", Vector3.zero);
    }

    private static Transform EnsureChild(Transform parent, string childName, Vector3 localPosition)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            child.localPosition = localPosition;
            return child;
        }

        GameObject obj = new GameObject(childName);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        return obj.transform;
    }

    private static void SetReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetBool(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static GameObject LoadVFXPrefab(string prefabName)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(VFXPrefabFolder + "/" + prefabName + ".prefab");
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
            return sprite;

        ConfigureTextureAsSprite(assetPath, DefaultPixelsPerUnit);
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static void ConfigureTextureAsSprite(string assetPath, float fallbackPixelsPerUnit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        float pixelsPerUnit = importer.spritePixelsPerUnit > 0f ? importer.spritePixelsPerUnit : fallbackPixelsPerUnit;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static Texture2D LoadTextureFromPng(string assetPath)
    {
        byte[] bytes = File.ReadAllBytes(ToFullPath(assetPath));
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
            throw new InvalidOperationException("Could not load PNG: " + assetPath);

        return texture;
    }

    private static void SetLoopTime(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    private static string GetSourcePath(string fileName)
    {
        return SourceFolder + "/" + fileName;
    }

    private static void EnsureFolder(string folder)
    {
        string normalized = folder.Replace("\\", "/");
        string[] parts = normalized.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private static void RequireDirectory(string assetPath)
    {
        if (!AssetDatabase.IsValidFolder(assetPath))
            throw new DirectoryNotFoundException(assetPath);
    }

    private static void RequireFile(string assetPath)
    {
        if (!File.Exists(ToFullPath(assetPath)))
            throw new FileNotFoundException(assetPath);
    }

    private static string ToFullPath(string assetPath)
    {
        return Path.GetFullPath(assetPath);
    }

    private readonly struct SheetSpec
    {
        public readonly string sourceFileName;
        public readonly string clipName;
        public readonly int frameCount;
        public readonly float frameRate;
        public readonly bool loop;

        public SheetSpec(string sourceFileName, string clipName, int frameCount, float frameRate, bool loop)
        {
            this.sourceFileName = sourceFileName;
            this.clipName = clipName;
            this.frameCount = frameCount;
            this.frameRate = frameRate;
            this.loop = loop;
        }
    }

    private readonly struct ClipBuildResult
    {
        public readonly AnimationClip clip;
        public readonly Sprite firstSprite;

        public ClipBuildResult(AnimationClip clip, Sprite firstSprite)
        {
            this.clip = clip;
            this.firstSprite = firstSprite;
        }
    }
}
