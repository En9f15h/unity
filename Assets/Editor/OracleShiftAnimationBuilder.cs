using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class OracleShiftAnimationBuilder
{
    private const string SourceFolder = "Assets/Resources/Character/Oracle/Animations";
    private const string CharacterFolder = "Assets/Resources/Character/Oracle";
    private const string ClipFolder = CharacterFolder + "/AnimationClips";
    private const string GeneratedSpriteFolder = ClipFolder + "/GeneratedSprites/OracleShift";
    private const string VFXControllerFolder = ClipFolder + "/VFXControllers";
    private const string VFXPrefabFolder = "Assets/Resources/Prefab/Oracle/vfx";
    private const string OraclePrefabPath = "Assets/Resources/Prefab/Oracle/Oracle_0.prefab";
    private const float DefaultPixelsPerUnit = 256f;
    private const float BodyDisappearLength = 0.5f;
    private const float BodyAppearLength = 0.4f;
    private const float ShiftFxFrameRate = 16f;
    private const float ShiftEnterFxLocalY = 0f;

    [MenuItem("Tools/Oracle/Build SHIFT Animation Assets")]
    public static void BuildShiftAnimationAssets()
    {
        Debug.Log("[Oracle SHIFT] Build started.");

        RequireDirectory(SourceFolder);
        RequireFile(OraclePrefabPath);
        EnsureFolder(ClipFolder);
        EnsureFolder(GeneratedSpriteFolder);
        EnsureFolder(VFXControllerFolder);
        EnsureFolder(VFXPrefabFolder);

        AnimationClip disappearClip = CreateBodyAlphaClip(
            "Oracle_Shift_Disappear",
            1f,
            0.2f,
            BodyDisappearLength,
            shrinkAtEnd: true);

        AnimationClip appearClip = CreateBodyAlphaClip(
            "Oracle_Shift_Appear",
            0.2f,
            1f,
            BodyAppearLength,
            shrinkAtEnd: false);

        ClipBuildResult exitFxClip = CreateSpriteClip(
            "Shift Vanish Animation.png",
            "Oracle_Shift_ExitFX",
            ShiftFxFrameRate);

        ClipBuildResult enterFxClip = CreateSpriteClip(
            "Shift Appear Animation.png",
            "Oracle_Shift_EnterFX",
            ShiftFxFrameRate);

        CreateAnimatedFxPrefab(
            "OracleShiftExitFX",
            exitFxClip,
            new Vector3(0.85f, 0.85f, 1f),
            1.1f);

        CreateAnimatedFxPrefab(
            "OracleShiftEnterFX",
            enterFxClip,
            new Vector3(0.85f, 0.85f, 1f),
            1.1f);

        AssignOraclePrefabReferences();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Oracle SHIFT] Found exit frames: " + exitFxClip.frameCount + ", enter frames: " + enterFxClip.frameCount + ".");
        Debug.Log("[Oracle SHIFT] Clips: " + AssetDatabase.GetAssetPath(disappearClip) + ", " + AssetDatabase.GetAssetPath(appearClip) + ", " + AssetDatabase.GetAssetPath(exitFxClip.clip) + ", " + AssetDatabase.GetAssetPath(enterFxClip.clip));
        Debug.Log("[Oracle SHIFT] Done.");
    }

    public static void BuildShiftAnimationAssetsFromCommandLine()
    {
        try
        {
            BuildShiftAnimationAssets();
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    private static AnimationClip CreateBodyAlphaClip(string clipName, float fromAlpha, float toAlpha, float length, bool shrinkAtEnd)
    {
        string clipPath = ClipFolder + "/" + clipName + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.name = clipName;
        clip.frameRate = 60f;
        clip.ClearCurves();

        AnimationCurve alpha = new AnimationCurve(
            new Keyframe(0f, fromAlpha),
            new Keyframe(Mathf.Max(0.01f, length), toAlpha));
        clip.SetCurve(string.Empty, typeof(SpriteRenderer), "m_Color.a", alpha);

        float middleScale = shrinkAtEnd ? 0.9f : 1.08f;
        AnimationCurve scale = new AnimationCurve(
            new Keyframe(0f, shrinkAtEnd ? 1f : 0.9f),
            new Keyframe(Mathf.Max(0.01f, length * 0.5f), middleScale),
            new Keyframe(Mathf.Max(0.01f, length), shrinkAtEnd ? 0.86f : 1f));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.x", scale);
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.y", scale);

        SetLoopTime(clip, false);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static ClipBuildResult CreateSpriteClip(string sourceFileName, string clipName, float frameRate)
    {
        string sourcePath = SourceFolder + "/" + sourceFileName;
        RequireFile(sourcePath);

        List<Sprite> sprites = LoadSpritesOrSlice(sourcePath, clipName);
        if (sprites.Count == 0)
            throw new InvalidOperationException("[Oracle SHIFT Builder] No SHIFT sprites found under: " + SourceFolder);

        sprites.Sort(CompareSpritesByNameAndFrame);

        string clipPath = ClipFolder + "/" + clipName + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.name = clipName;
        clip.frameRate = frameRate;
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
                time = i / frameRate,
                value = sprites[i]
            };
        }

        keyframes[sprites.Count] = new ObjectReferenceKeyframe
        {
            time = sprites.Count / frameRate,
            value = sprites[sprites.Count - 1]
        };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        SetLoopTime(clip, false);
        EditorUtility.SetDirty(clip);

        return new ClipBuildResult(clip, sprites[0], sprites.Count);
    }

    private static List<Sprite> LoadSpritesOrSlice(string sourcePath, string clipName)
    {
        List<Sprite> sprites = new List<Sprite>();
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(sourcePath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                sprites.Add(sprite);
        }

        if (sprites.Count > 1)
            return sprites;

        ConfigureTextureAsSprite(sourcePath, DefaultPixelsPerUnit);
        Texture2D source = LoadTextureFromPng(sourcePath);
        try
        {
            int frameCount = GuessHorizontalFrameCount(source);
            int frameWidth = source.width / frameCount;
            int frameHeight = source.height;

            string frameFolder = GeneratedSpriteFolder + "/" + clipName;
            EnsureFolder(frameFolder);
            sprites.Clear();

            for (int i = 0; i < frameCount; i++)
            {
                Texture2D frame = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false);
                frame.SetPixels(source.GetPixels(i * frameWidth, 0, frameWidth, frameHeight));
                frame.Apply();

                string framePath = frameFolder + "/" + clipName + "_" + i.ToString("00") + ".png";
                File.WriteAllBytes(ToFullPath(framePath), frame.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(frame);

                AssetDatabase.ImportAsset(framePath, ImportAssetOptions.ForceUpdate);
                ConfigureTextureAsSprite(framePath, DefaultPixelsPerUnit);

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
                if (sprite != null)
                    sprites.Add(sprite);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }

        return sprites;
    }

    private static int GuessHorizontalFrameCount(Texture2D source)
    {
        if (source == null || source.height <= 0)
            return 1;

        if (source.width > source.height && source.width % source.height == 0)
            return Mathf.Max(1, source.width / source.height);

        return 1;
    }

    private static void CreateAnimatedFxPrefab(string prefabName, ClipBuildResult clipResult, Vector3 scale, float lifeTime)
    {
        string prefabPath = VFXPrefabFolder + "/" + prefabName + ".prefab";
        string controllerPath = VFXControllerFolder + "/" + prefabName + ".controller";
        AnimatorController controller = CreateSingleClipController(controllerPath, clipResult.clip);

        GameObject root = new GameObject(prefabName);
        try
        {
            if (prefabName == "OracleShiftEnterFX")
                root.transform.localPosition = new Vector3(0f, ShiftEnterFxLocalY, 0f);

            root.transform.localScale = scale;

            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visualRoot.AddComponent<SpriteRenderer>();
            renderer.sprite = clipResult.firstSprite;

            Animator animator = visualRoot.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            AutoDestroyEffect autoDestroy = root.AddComponent<AutoDestroyEffect>();
            SerializedObject serialized = new SerializedObject(autoDestroy);
            SerializedProperty property = serialized.FindProperty("lifeTime");
            if (property != null)
                property.floatValue = lifeTime;

            property = serialized.FindProperty("useUnscaledTime");
            if (property != null)
                property.boolValue = true;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log("[Oracle SHIFT] Updated prefab: " + prefabPath);
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

        AnimatorStateTransition[] transitions = stateMachine.anyStateTransitions;
        for (int i = transitions.Length - 1; i >= 0; i--)
            stateMachine.RemoveAnyStateTransition(transitions[i]);
    }

    private static void AssignOraclePrefabReferences()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(OraclePrefabPath);
        try
        {
            OracleVFXController controller = root.GetComponent<OracleVFXController>();
            if (controller == null)
                controller = root.AddComponent<OracleVFXController>();

            SerializedObject serialized = new SerializedObject(controller);
            SetReference(serialized, "shiftExitFXPrefab", LoadVFXPrefab("OracleShiftExitFX"));
            SetReference(serialized, "shiftEnterFXPrefab", LoadVFXPrefab("OracleShiftEnterFX"));
            SetFloat(serialized, "shiftEnterYOffset", ShiftEnterFxLocalY);
            SetBool(serialized, "useWardPrefabOnly", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, OraclePrefabPath);
            Debug.Log("[Oracle SHIFT] Oracle prefab SHIFT references assigned: " + OraclePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
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

    private static int CompareSpritesByNameAndFrame(Sprite left, Sprite right)
    {
        int leftFrame = ExtractLastNumber(left != null ? left.name : string.Empty);
        int rightFrame = ExtractLastNumber(right != null ? right.name : string.Empty);

        if (leftFrame >= 0 && rightFrame >= 0 && leftFrame != rightFrame)
            return leftFrame.CompareTo(rightFrame);

        return string.Compare(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static int ExtractLastNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
            return -1;

        int end = -1;
        for (int i = value.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(value[i]))
            {
                end = i;
                break;
            }
        }

        if (end < 0)
            return -1;

        int start = end;
        while (start > 0 && char.IsDigit(value[start - 1]))
            start--;

        return int.TryParse(value.Substring(start, end - start + 1), out int result) ? result : -1;
    }

    private static void ConfigureTextureAsSprite(string assetPath, float fallbackPixelsPerUnit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        float pixelsPerUnit = importer.spritePixelsPerUnit > 0f ? importer.spritePixelsPerUnit : fallbackPixelsPerUnit;
        importer.textureType = TextureImporterType.Sprite;
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

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string[] parts = assetPath.Split('/');
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
            throw new DirectoryNotFoundException("[Oracle SHIFT Builder] Directory not found: " + assetPath);
    }

    private static void RequireFile(string assetPath)
    {
        if (!File.Exists(ToFullPath(assetPath)))
            throw new FileNotFoundException("[Oracle SHIFT Builder] File not found: " + assetPath, assetPath);
    }

    private static string ToFullPath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private readonly struct ClipBuildResult
    {
        public readonly AnimationClip clip;
        public readonly Sprite firstSprite;
        public readonly int frameCount;

        public ClipBuildResult(AnimationClip clip, Sprite firstSprite, int frameCount)
        {
            this.clip = clip;
            this.firstSprite = firstSprite;
            this.frameCount = frameCount;
        }
    }
}
