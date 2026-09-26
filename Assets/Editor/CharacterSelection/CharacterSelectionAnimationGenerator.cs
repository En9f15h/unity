using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CharacterSelectionAnimationGenerator
{
    public const string AnimationFolder = "Assets/Generated/CharacterSelection/Animations";
    public const string ControllerFolder = "Assets/Generated/CharacterSelection/Controllers";

    private static readonly string[] ClipNames =
    {
        "Card_HoverIn",
        "Card_HoverOut",
        "Card_SelectedPulse",
        "Character_Previous",
        "Character_Next",
        "Character_RevealLeft",
        "Character_RevealRight",
        "Character_Hide",
        "Radar_Reveal",
        "Radar_Hide",
        "Ready_Enter",
        "Ready_Loop",
        "VS_Flash",
        "VS_Reveal",
        "Countdown_Pop",
        "Countdown_Fight",
        "MapCard_HoverIn",
        "MapCard_HoverOut",
        "MapCard_Selected",
        "Map_Previous",
        "Map_Next",
        "Fog_DriftLeft",
        "Fog_DriftRight",
        "Scene_FadeIn",
        "Scene_FadeOut"
    };

    [MenuItem("Tools/Character Selection/Generate Placeholder Assets")]
    public static void GeneratePlaceholderAssets()
    {
        CharacterSelectionTextureGenerator.GenerateTextures();
        GenerateAnimations();
        CharacterSelectionParticleGenerator.GenerateParticlePrefabs();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Character Selection placeholder assets generated.");
    }

    public static void GenerateFromCommandLine()
    {
        GeneratePlaceholderAssets();
        EditorApplication.Exit(0);
    }

    public static Dictionary<string, AnimationClip> GenerateAnimations()
    {
        CharacterSelectionTextureGenerator.EnsureFolder(AnimationFolder);
        CharacterSelectionTextureGenerator.EnsureFolder(ControllerFolder);

        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        for (int i = 0; i < ClipNames.Length; i++)
        {
            AnimationClip clip = CreateClip(ClipNames[i]);
            string path = AnimationFolder + "/" + ClipNames[i] + ".anim";
            ReplaceAsset(path, clip);
            clips[ClipNames[i]] = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        CreateController("CharacterSelection_UI.controller", clips);
        return clips;
    }

    private static AnimationClip CreateClip(string clipName)
    {
        AnimationClip clip = new AnimationClip
        {
            name = clipName,
            frameRate = 60f
        };

        bool loop = clipName == "Ready_Loop" || clipName == "Fog_DriftLeft" || clipName == "Fog_DriftRight";
        if (loop)
        {
            SerializedObject serializedClip = new SerializedObject(clip);
            SerializedProperty settings = serializedClip.FindProperty("m_AnimationClipSettings");
            if (settings != null)
            {
                SerializedProperty loopTime = settings.FindPropertyRelative("m_LoopTime");
                if (loopTime != null)
                    loopTime.boolValue = true;
            }
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
        }

        float duration = loop ? 1f : 0.35f;
        if (clipName.Contains("Reveal"))
            duration = 0.6f;
        if (clipName.Contains("Scene_Fade"))
            duration = 0.5f;

        AnimationCurve scaleX = AnimationCurve.Linear(0f, GetStartScale(clipName), duration, GetEndScale(clipName));
        AnimationCurve scaleY = AnimationCurve.Linear(0f, GetStartScale(clipName), duration, GetEndScale(clipName));
        AnimationCurve scaleZ = AnimationCurve.Linear(0f, 1f, duration, 1f);
        AnimationCurve alpha = AnimationCurve.Linear(0f, GetStartAlpha(clipName), duration, GetEndAlpha(clipName));
        AnimationCurve posX = AnimationCurve.Linear(0f, GetStartX(clipName), duration, GetEndX(clipName));

        SetCurve(clip, typeof(Transform), "m_LocalScale.x", scaleX);
        SetCurve(clip, typeof(Transform), "m_LocalScale.y", scaleY);
        SetCurve(clip, typeof(Transform), "m_LocalScale.z", scaleZ);
        SetCurve(clip, typeof(RectTransform), "m_AnchoredPosition.x", posX);
        SetCurve(clip, typeof(CanvasGroup), "m_Alpha", alpha);

        return clip;
    }

    private static float GetStartScale(string name)
    {
        if (name.Contains("HoverIn") || name.Contains("Selected") || name.Contains("Pop") || name.Contains("Fight"))
            return 1f;
        if (name.Contains("Reveal") || name.Contains("Ready_Enter"))
            return 0.85f;
        return 1f;
    }

    private static float GetEndScale(string name)
    {
        if (name.Contains("HoverIn") || name.Contains("Selected") || name.Contains("Pop") || name.Contains("Fight"))
            return 1.08f;
        if (name.Contains("HoverOut"))
            return 1f;
        return 1f;
    }

    private static float GetStartAlpha(string name)
    {
        if (name.Contains("Hide") || name.Contains("FadeOut"))
            return 1f;
        if (name.Contains("Reveal") || name.Contains("FadeIn") || name.Contains("Enter"))
            return 0f;
        return 1f;
    }

    private static float GetEndAlpha(string name)
    {
        if (name.Contains("Hide") || name.Contains("FadeOut"))
            return 0f;
        return 1f;
    }

    private static float GetStartX(string name)
    {
        if (name.Contains("RevealLeft") || name.Contains("DriftLeft"))
            return -20f;
        if (name.Contains("RevealRight") || name.Contains("DriftRight"))
            return 20f;
        return 0f;
    }

    private static float GetEndX(string name)
    {
        if (name.Contains("DriftLeft"))
            return -80f;
        if (name.Contains("DriftRight"))
            return 80f;
        return 0f;
    }

    private static void SetCurve(AnimationClip clip, System.Type type, string propertyName, AnimationCurve curve)
    {
        EditorCurveBinding binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = type,
            propertyName = propertyName
        };
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static void CreateController(string fileName, Dictionary<string, AnimationClip> clips)
    {
        string path = ControllerFolder + "/" + fileName;
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = stateMachine.AddState("Idle");
        stateMachine.defaultState = idleState;

        foreach (KeyValuePair<string, AnimationClip> pair in clips)
        {
            if (pair.Value == null)
                continue;

            string triggerName = pair.Key.Replace("_", string.Empty);
            controller.AddParameter(triggerName, AnimatorControllerParameterType.Trigger);
            AnimatorState state = stateMachine.AddState(pair.Key);
            state.motion = pair.Value;
            state.writeDefaultValues = true;

            AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = true;
            transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
        }

        EditorUtility.SetDirty(controller);
    }

    private static void ReplaceAsset(string path, Object asset)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AssetDatabase.CreateAsset(asset, path);
    }
}
