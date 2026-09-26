using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class OracleAnimatorSetup
{
    private const string PrefabPath = "Assets/Resources/Prefab/Oracle/Oracle_0.prefab";
    private const string CharacterFolder = "Assets/Resources/Character/Oracle";
    private const string AnimationFolder = CharacterFolder + "/AnimationClips";
    private const string ControllerPath = AnimationFolder + "/Oracle_Animator.controller";

    private static readonly string[] TriggerNames =
    {
        "MoveForward",
        "MoveBackward",
        "Jump",
        "LightAttack",
        "HeavyCharge",
        "HeavyAttack",
        "LowAttack",
        "Parry",
        "Bolt",
        "RiftCharge",
        "RiftRelease",
        "Shift",
        "Ward",
        "Fade",
        "Sight",
        "ParryCounter",
        "Defense",
        "Hit",
        "Dance",
        "Ultimate"
    };

    [MenuItem("Tools/Oracle/Build Animator")]
    public static void BuildAnimator()
    {
        EnsureFolder(AnimationFolder);

        AnimationClip idle = CreateClipIfMissing("Idle", true, BuildIdle);
        AnimatorController controller = CreateController(idle);
        AssignControllerToPrefab(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Oracle Animator setup completed.");
    }

    private static AnimatorController CreateController(AnimationClip idle)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        controller.parameters = new AnimatorControllerParameter[0];
        for (int i = 0; i < TriggerNames.Length; i++)
            controller.AddParameter(TriggerNames[i], AnimatorControllerParameterType.Trigger);

        AnimatorControllerLayer layer = controller.layers[0];
        AnimatorStateMachine stateMachine = layer.stateMachine;
        ClearStateMachine(stateMachine);

        AnimatorState idleState = stateMachine.AddState("Idle", new Vector3(250f, 120f, 0f));
        idleState.motion = idle;
        stateMachine.defaultState = idleState;

        for (int i = 0; i < TriggerNames.Length; i++)
        {
            string stateName = TriggerNames[i];
            AnimationClip clip = CreateClipIfMissing(stateName, false, builder => BuildActionClip(builder, stateName));
            AnimatorState state = stateMachine.AddState(stateName, new Vector3(250f, 200f + i * 70f, 0f));
            state.motion = clip;

            AnimatorStateTransition enter = stateMachine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0f;
            enter.hasFixedDuration = true;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, stateName);

            AnimatorStateTransition exit = state.AddTransition(idleState);
            exit.hasExitTime = true;
            exit.exitTime = 1f;
            exit.duration = 0f;
            exit.hasFixedDuration = true;
        }

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

    private static AnimationClip CreateClipIfMissing(string name, bool loop, System.Action<AnimationClip> build)
    {
        string path = AnimationFolder + "/" + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip != null)
        {
            if (IsClipEmpty(clip))
            {
                clip.frameRate = 60f;
                clip.name = name;
                clip.ClearCurves();
                build(clip);
                SetLoopTime(clip, loop);
                EditorUtility.SetDirty(clip);
                Debug.Log("[OracleAnimatorSetup] Rebuilt empty clip: " + path);
            }

            return clip;
        }

        clip = new AnimationClip
        {
            frameRate = 60f,
            name = name
        };

        build(clip);
        SetLoopTime(clip, loop);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static bool IsClipEmpty(AnimationClip clip)
    {
        return AnimationUtility.GetCurveBindings(clip).Length == 0
            && AnimationUtility.GetObjectReferenceCurveBindings(clip).Length == 0;
    }

    private static void BuildIdle(AnimationClip clip)
    {
        AddPositionY(clip, 0f, 0f, 0.5f, 0.045f, 1f, 0f);
        AddRotationZ(clip, 0f, -1.5f, 0.5f, 1.5f, 1f, -1.5f);
    }

    private static void BuildActionClip(AnimationClip clip, string stateName)
    {
        switch (stateName)
        {
            case "MoveForward":
                AddPositionX(clip, 0f, 0f, 0.18f, 0.08f, 0.36f, 0f);
                AddRotationZ(clip, 0f, 0f, 0.18f, -4f, 0.36f, 0f);
                break;
            case "MoveBackward":
                AddPositionX(clip, 0f, 0f, 0.18f, -0.08f, 0.36f, 0f);
                AddRotationZ(clip, 0f, 0f, 0.18f, 4f, 0.36f, 0f);
                break;
            case "Jump":
                AddPositionY(clip, 0f, 0f, 0.25f, 0.35f, 0.5f, 0f);
                break;
            case "LightAttack":
                AddPositionX(clip, 0f, 0f, 0.12f, 0.12f, 0.3f, 0f);
                AddRotationZ(clip, 0f, 0f, 0.12f, -5f, 0.3f, 0f);
                break;
            case "HeavyCharge":
                AddPositionY(clip, 0f, 0f, 0.5f, 0.05f, 1f, 0f);
                AddScale(clip, 0f, 1f, 0.5f, 1.08f, 1f, 1f);
                break;
            case "HeavyAttack":
                AddPositionX(clip, 0f, 0f, 0.16f, 0.18f, 0.42f, -0.04f, 0.6f, 0f);
                AddRotationZ(clip, 0f, 0f, 0.16f, -9f, 0.6f, 0f);
                break;
            case "LowAttack":
                AddPositionY(clip, 0f, 0f, 0.16f, -0.1f, 0.36f, 0f);
                AddRotationZ(clip, 0f, 0f, 0.16f, 5f, 0.36f, 0f);
                break;
            case "Parry":
                AddPositionX(clip, 0f, 0f, 0.12f, -0.06f, 0.4f, 0f);
                AddScale(clip, 0f, 1f, 0.12f, 0.96f, 0.4f, 1f);
                break;
            case "Bolt":
                AddPositionX(clip, 0f, 0f, 0.12f, 0.14f, 0.28f, 0f);
                AddScale(clip, 0f, 1f, 0.12f, 1.06f, 0.28f, 1f);
                break;
            case "RiftCharge":
                AddPositionY(clip, 0f, 0f, 0.5f, 0.04f, 1f, 0f);
                AddScale(clip, 0f, 1f, 0.5f, 1.08f, 1f, 1f);
                break;
            case "RiftRelease":
                AddPositionY(clip, 0f, 0f, 0.25f, 0.12f, 0.5f, -0.04f, 1f, 0f);
                AddScale(clip, 0f, 1f, 0.5f, 1.15f, 1f, 1f);
                break;
            case "Shift":
                AddPositionX(clip, 0f, 0f, 0.22f, -0.08f, 0.5f, 0.08f, 1f, 0f);
                AddScale(clip, 0f, 1f, 0.5f, 0.86f, 1f, 1f);
                break;
            case "Ward":
                AddPositionX(clip, 0f, 0f, 0.16f, -0.06f, 0.5f, -0.04f, 1f, 0f);
                AddScale(clip, 0f, 1f, 0.2f, 0.95f, 1f, 1f);
                break;
            case "Fade":
                AddPositionX(clip, 0f, 0f, 0.2f, -0.05f, 0.5f, 0.05f, 1f, 0f);
                AddScale(clip, 0f, 1f, 0.2f, 0.92f, 1f, 1f);
                break;
            case "Sight":
                AddPositionY(clip, 0f, 0f, 0.5f, 0.16f, 1f, 0f);
                AddScale(clip, 0f, 1f, 0.5f, 1.12f, 1f, 1f);
                break;
            case "ParryCounter":
                AddPositionX(clip, 0f, 0f, 0.12f, 0.2f, 0.36f, 0f);
                AddRotationZ(clip, 0f, 0f, 0.12f, -10f, 0.36f, 0f);
                break;
            case "Defense":
                AddPositionX(clip, 0f, 0f, 0.14f, -0.08f, 0.4f, 0f);
                AddScale(clip, 0f, 1f, 0.14f, 0.94f, 0.4f, 1f);
                break;
            case "Hit":
                AddPositionX(clip, 0f, 0f, 0.06f, -0.08f, 0.12f, 0.06f, 0.2f, 0f);
                AddRotationZ(clip, 0f, 0f, 0.06f, 5f, 0.2f, 0f);
                break;
            case "Dance":
                AddPositionY(clip, 0f, 0f, 0.18f, 0.08f, 0.36f, 0f, 0.54f, 0.08f, 0.72f, 0f);
                AddRotationZ(clip, 0f, 0f, 0.18f, -7f, 0.36f, 7f, 0.54f, -7f, 0.72f, 0f);
                break;
            case "Ultimate":
                AddPositionY(clip, 0f, 0f, 0.25f, 0.18f, 0.5f, 0f);
                AddScale(clip, 0f, 1f, 0.25f, 1.16f, 0.5f, 1f);
                AddRotationZ(clip, 0f, 0f, 0.25f, -14f, 0.5f, 0f);
                break;
        }
    }

    private static void AssignControllerToPrefab(AnimatorController controller)
    {
        if (!File.Exists(PrefabPath))
        {
            Debug.LogWarning("Oracle prefab not found: " + PrefabPath);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform visualRoot = root.transform.Find("visualRoot");
            if (visualRoot == null)
            {
                GameObject visual = new GameObject("visualRoot");
                visualRoot = visual.transform;
                visualRoot.SetParent(root.transform, false);
            }

            SpriteRenderer rootSprite = root.GetComponent<SpriteRenderer>();
            SpriteRenderer visualSprite = visualRoot.GetComponent<SpriteRenderer>();
            if (visualSprite == null)
                visualSprite = visualRoot.gameObject.AddComponent<SpriteRenderer>();

            if (rootSprite != null)
            {
                EditorUtility.CopySerialized(rootSprite, visualSprite);
                Object.DestroyImmediate(rootSprite);
            }

            Animator rootAnimator = root.GetComponent<Animator>();
            if (rootAnimator != null)
                Object.DestroyImmediate(rootAnimator);

            Animator visualAnimator = visualRoot.GetComponent<Animator>();
            if (visualAnimator == null)
                visualAnimator = visualRoot.gameObject.AddComponent<Animator>();

            visualAnimator.runtimeAnimatorController = controller;
            visualAnimator.applyRootMotion = false;

            AssignSerializedObjectReference(root, "CharacterUnit", "animatorOverride", visualAnimator);
            AssignSerializedObjectReference(root, "CharacterFacing", "visualRoot", visualRoot);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AssignSerializedObjectReference(GameObject root, string typeName, string fieldName, Object value)
    {
        MonoBehaviour[] behaviours = root.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null || behaviours[i].GetType().Name != typeName)
                continue;

            SerializedObject serialized = new SerializedObject(behaviours[i]);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property != null)
            {
                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static void SetLoopTime(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    private static void AddPositionX(AnimationClip clip, params float[] keys)
    {
        AddCurve(clip, "m_LocalPosition.x", keys);
    }

    private static void AddPositionY(AnimationClip clip, params float[] keys)
    {
        AddCurve(clip, "m_LocalPosition.y", keys);
    }

    private static void AddRotationZ(AnimationClip clip, params float[] keys)
    {
        AddCurve(clip, "localEulerAnglesRaw.z", keys);
    }

    private static void AddScale(AnimationClip clip, params float[] keys)
    {
        AddCurve(clip, "m_LocalScale.x", keys);
        AddCurve(clip, "m_LocalScale.y", keys);
    }

    private static void AddCurve(AnimationClip clip, string propertyName, params float[] keys)
    {
        AnimationCurve curve = new AnimationCurve();
        for (int i = 0; i + 1 < keys.Length; i += 2)
            curve.AddKey(keys[i], keys[i + 1]);

        clip.SetCurve(string.Empty, typeof(Transform), propertyName, curve);
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
}
