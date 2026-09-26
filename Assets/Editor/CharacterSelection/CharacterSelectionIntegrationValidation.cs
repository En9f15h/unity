using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CharacterSelectionIntegrationValidation
{
    private const string CharacterSelectScenePath = "Assets/Scenes/CharacterSelectScene.unity";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string GeneratedRoot = "Assets/Generated/CharacterSelection";

    private static readonly string[] RequiredAnimationNames =
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

    private static readonly string[] RequiredTextureNames =
    {
        "SoftGlow",
        "LightningStrip",
        "PurpleFogNoise",
        "RadarPoint",
        "WhiteFlash",
        "Vignette"
    };

    private static readonly string[] RequiredParticlePrefabNames =
    {
        "ReadyAura",
        "RevealBurst",
        "LightningBurst",
        "PurpleDust"
    };

    [MenuItem("Tools/Character Selection/Validate Integration")]
    public static void ValidateMenu()
    {
        if (ValidateAll())
            Debug.Log("Character Selection integration validation passed.");
    }

    public static void ValidateFromCommandLine()
    {
        bool passed = ValidateAll();
        EditorApplication.Exit(passed ? 0 : 1);
    }

    public static bool ValidateAll()
    {
        List<string> errors = new List<string>();

        ValidateGeneratedAssets(errors);
        ValidateDefinitions(errors);
        ValidateCharacterSelectScene(errors);
        ValidateGameScene(errors);
        ValidateBuildSettings(errors);

        if (errors.Count == 0)
        {
            Debug.Log("Character Selection integration validation passed.");
            return true;
        }

        for (int i = 0; i < errors.Count; i++)
            Debug.LogError("Character Selection validation: " + errors[i]);

        return false;
    }

    private static void ValidateGeneratedAssets(List<string> errors)
    {
        for (int i = 0; i < RequiredAnimationNames.Length; i++)
        {
            string path = GeneratedRoot + "/Animations/" + RequiredAnimationNames[i] + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                errors.Add("missing animation clip: " + path);
                continue;
            }

            if (clip.length <= 0f || AnimationUtility.GetCurveBindings(clip).Length == 0)
                errors.Add("animation clip has no useful curves: " + path);
        }

        for (int i = 0; i < RequiredTextureNames.Length; i++)
        {
            string path = GeneratedRoot + "/Textures/" + RequiredTextureNames[i] + ".png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                errors.Add("missing generated texture: " + path);
        }

        for (int i = 0; i < RequiredParticlePrefabNames.Length; i++)
        {
            string path = GeneratedRoot + "/Prefabs/" + RequiredParticlePrefabNames[i] + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                errors.Add("missing particle prefab: " + path);
                continue;
            }

            if (prefab.GetComponentInChildren<ParticleSystem>(true) == null)
                errors.Add("particle prefab has no ParticleSystem: " + path);
        }

        ValidateAnimatorController(errors);
        ValidateAnimatedUIPrefab(GeneratedRoot + "/Prefabs/CharacterSelectionCard.prefab", typeof(CharacterSelectionCard), errors);
        ValidateAnimatedUIPrefab(GeneratedRoot + "/Prefabs/MapSelectionCard.prefab", typeof(MapSelectionCard), errors);
    }

    private static void ValidateAnimatorController(List<string> errors)
    {
        string path = GeneratedRoot + "/Controllers/CharacterSelection_UI.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            errors.Add("missing animator controller: " + path);
            return;
        }

        if (controller.layers.Length == 0 ||
            controller.layers[0].stateMachine == null ||
            controller.layers[0].stateMachine.defaultState == null ||
            controller.layers[0].stateMachine.defaultState.name != "Idle")
        {
            errors.Add("animator controller default state must be neutral Idle.");
        }

        for (int i = 0; i < RequiredAnimationNames.Length; i++)
        {
            string triggerName = RequiredAnimationNames[i].Replace("_", string.Empty);
            if (!HasTriggerParameter(controller, triggerName))
                errors.Add("animator controller missing trigger parameter: " + triggerName);

            if (!HasAnyStateTransition(controller, triggerName))
                errors.Add("animator controller missing Any State transition for trigger: " + triggerName);
        }
    }

    private static void ValidateAnimatedUIPrefab(string path, System.Type expectedComponent, List<string> errors)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            errors.Add("missing UI prefab: " + path);
            return;
        }

        Component cardComponent = prefab.GetComponent(expectedComponent);
        if (cardComponent == null)
            errors.Add("UI prefab missing component " + expectedComponent.Name + ": " + path);
        else
            RequireObject(new SerializedObject(cardComponent), "button", errors);

        Button button = prefab.GetComponent<Button>();
        if (button == null)
            errors.Add("UI prefab missing Button click target: " + path);

        Image image = prefab.GetComponent<Image>();
        if (image == null || !image.raycastTarget)
            errors.Add("UI prefab root Image must be the raycast target: " + path);

        Animator animator = prefab.GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
            errors.Add("UI prefab missing generated Animator controller: " + path);

        if (prefab.GetComponent<CanvasGroup>() == null)
            errors.Add("UI prefab missing CanvasGroup for generated alpha animations: " + path);
    }

    private static void ValidateDefinitions(List<string> errors)
    {
        CharacterSelectionDefinition knight = AssetDatabase.LoadAssetAtPath<CharacterSelectionDefinition>(GeneratedRoot + "/Definitions/KnightSelectionDefinition.asset");
        CharacterSelectionDefinition oracle = AssetDatabase.LoadAssetAtPath<CharacterSelectionDefinition>(GeneratedRoot + "/Definitions/OracleSelectionDefinition.asset");

        ValidateCharacterDefinition(knight, "knight", errors);
        ValidateCharacterDefinition(oracle, "oracle", errors);

        MapSelectionDefinition storm = AssetDatabase.LoadAssetAtPath<MapSelectionDefinition>(GeneratedRoot + "/Definitions/Map_StormField.asset");
        MapSelectionDefinition astral = AssetDatabase.LoadAssetAtPath<MapSelectionDefinition>(GeneratedRoot + "/Definitions/Map_AstralRuins.asset");

        ValidateMapDefinition(storm, "storm_field", errors);
        ValidateMapDefinition(astral, "astral_ruins", errors);
    }

    private static void ValidateCharacterDefinition(CharacterSelectionDefinition definition, string expectedId, List<string> errors)
    {
        if (definition == null)
        {
            errors.Add("missing character definition for " + expectedId);
            return;
        }

        if (definition.characterId != expectedId)
            errors.Add("characterId mismatch for " + expectedId);

        if (string.IsNullOrEmpty(definition.shortRole))
            errors.Add("shortRole is required for " + expectedId);

        if (definition.combatConfig == null)
            errors.Add("missing combat config reference for " + expectedId);

        if (definition.previewPrefab == null)
            errors.Add("missing UI preview prefab for " + expectedId);
        else if (definition.previewPrefab.GetComponent<RectTransform>() == null || definition.previewPrefab.GetComponent<Image>() == null)
            errors.Add("UI preview prefab must be a UGUI prefab for " + expectedId);

        if (definition.skinPreviewPrefabs == null || definition.skinPreviewPrefabs.Length == 0)
        {
            errors.Add("missing skin UI preview prefabs for " + expectedId);
        }
        else
        {
            for (int i = 0; i < definition.skinPreviewPrefabs.Length; i++)
            {
                GameObject prefab = definition.skinPreviewPrefabs[i];
                if (prefab == null)
                    errors.Add("skin UI preview prefab is null for " + expectedId + " at index " + i);
                else if (prefab.GetComponent<RectTransform>() == null || prefab.GetComponent<Image>() == null)
                    errors.Add("skin preview prefab must be a UGUI prefab for " + expectedId + " at index " + i);
            }
        }

        if (definition.skills == null || definition.skills.Length == 0)
            errors.Add("missing skill popup data for " + expectedId);
    }

    private static void ValidateMapDefinition(MapSelectionDefinition definition, string expectedId, List<string> errors)
    {
        if (definition == null)
        {
            errors.Add("missing map definition for " + expectedId);
            return;
        }

        if (definition.mapId != expectedId)
            errors.Add("mapId mismatch for " + expectedId);

        if (definition.mapPrefab == null)
            errors.Add("missing map prefab reference for " + expectedId);
    }

    private static void ValidateCharacterSelectScene(List<string> errors)
    {
        if (!LoadScene(CharacterSelectScenePath, errors))
            return;

        CharacterSelectionManager[] managers = GetSceneComponents<CharacterSelectionManager>();
        if (managers.Length != 1)
        {
            errors.Add("CharacterSelectScene must contain exactly one CharacterSelectionManager.");
            return;
        }

        CharacterSelectionManager manager = managers[0];
        SerializedObject serializedManager = new SerializedObject(manager);
        RequireObject(serializedManager, "characterSelector", errors);
        RequireObject(serializedManager, "mapSelectorController", errors);
        RequireObject(serializedManager, "youPanel", errors);
        RequireObject(serializedManager, "enemyPanel", errors);
        RequireObject(serializedManager, "privacyController", errors);
        RequireObject(serializedManager, "phaseOverlay", errors);
        RequireObject(serializedManager, "revealText", errors);
        RequireObject(serializedManager, "countdownText", errors);
        RequireObject(serializedManager, "fightText", errors);

        RequireObjectArray(serializedManager, "characters", 2, errors);
        RequireObjectArray(serializedManager, "maps", 1, errors);

        SerializedProperty battleSceneName = serializedManager.FindProperty("battleSceneName");
        if (battleSceneName == null || battleSceneName.stringValue != "GameScene")
            errors.Add("CharacterSelectionManager battleSceneName must be GameScene.");

        CanvasScaler scaler = GetFirstSceneComponent<CanvasScaler>();
        if (scaler == null)
        {
            errors.Add("CharacterSelectScene is missing CanvasScaler.");
        }
        else
        {
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                errors.Add("CanvasScaler must use Scale With Screen Size.");
            if (scaler.referenceResolution != new Vector2(1920f, 1080f))
                errors.Add("CanvasScaler reference resolution must be 1920 x 1080.");
            if (Mathf.Abs(scaler.matchWidthOrHeight - 0.5f) > 0.001f)
                errors.Add("CanvasScaler match must be 0.5.");
        }

        Camera camera = GetFirstSceneComponent<Camera>();
        if (camera == null)
            errors.Add("CharacterSelectScene is missing a Camera.");

        StandaloneInputModule oldInputModule = GetFirstSceneComponent<StandaloneInputModule>();
        if (oldInputModule != null)
            errors.Add("CharacterSelectScene uses StandaloneInputModule, but this project uses the Input System package.");

        InputSystemUIInputModule inputSystemModule = GetFirstSceneComponent<InputSystemUIInputModule>();
        if (inputSystemModule == null)
            errors.Add("CharacterSelectScene is missing InputSystemUIInputModule.");

        CharacterSelectorController[] selectors = GetSceneComponents<CharacterSelectorController>();
        if (selectors.Length != 1)
        {
            errors.Add("CharacterSelectScene must contain exactly one CharacterSelectorController.");
        }
        else
        {
            SerializedObject serializedSelector = new SerializedObject(selectors[0]);
            RequireObject(serializedSelector, "targetPanel", errors);
            RequireObject(serializedSelector, "previousCharacterButton", errors);
            RequireObject(serializedSelector, "nextCharacterButton", errors);
            RequireObject(serializedSelector, "previousSkinButton", errors);
            RequireObject(serializedSelector, "nextSkinButton", errors);

            SerializedProperty targetPanelProperty = serializedSelector.FindProperty("targetPanel");
            SerializedProperty animationControllerProperty = serializedSelector.FindProperty("animationController");
            Component targetPanel = targetPanelProperty != null ? targetPanelProperty.objectReferenceValue as Component : null;
            Component animationController = animationControllerProperty != null ? animationControllerProperty.objectReferenceValue as Component : null;
            if (targetPanel != null && animationController != null && animationController.transform == targetPanel.transform)
                errors.Add("CharacterSelectorController.animationController must not point at the target player panel root.");
        }

        MapSelectorController[] mapSelectors = GetSceneComponents<MapSelectorController>();
        if (mapSelectors.Length != 1)
        {
            errors.Add("CharacterSelectScene must contain exactly one MapSelectorController.");
        }
        else
        {
            SerializedObject serializedMapSelector = new SerializedObject(mapSelectors[0]);
            RequireObject(serializedMapSelector, "previousMapButton", errors);
            RequireObject(serializedMapSelector, "nextMapButton", errors);
            RequireObject(serializedMapSelector, "mapPreviewImage", errors);
            RequireObject(serializedMapSelector, "currentMapNameText", errors);
            RequireObject(serializedMapSelector, "hostOnlyLockImage", errors);
        }

        CharacterSelectionUILayoutController layoutController = GetFirstSceneComponent<CharacterSelectionUILayoutController>();
        if (layoutController == null)
        {
            errors.Add("CharacterSelectCanvas is missing CharacterSelectionUILayoutController.");
        }
        else
        {
            SerializedObject serializedLayout = new SerializedObject(layoutController);
            RequireObject(serializedLayout, "topBar", errors);
            RequireObject(serializedLayout, "youPanel", errors);
            RequireObject(serializedLayout, "enemyPanel", errors);
            RequireObject(serializedLayout, "centerPanel", errors);
            RequireObject(serializedLayout, "mapSelectorPanel", errors);
            RequireObject(serializedLayout, "phaseOverlay", errors);
            RequireObject(serializedLayout, "sceneFade", errors);
        }

        RequireSceneAnimator("CenterPanel", errors);
        RequireSceneAnimator("SceneFade", errors);

        CharacterSelectionCard[] characterCards = GetSceneComponents<CharacterSelectionCard>();
        if (characterCards.Length > 0)
            errors.Add("CharacterSelectScene must not use CharacterSelectionCard components.");

        MapSelectionCard[] mapCards = GetSceneComponents<MapSelectionCard>();
        if (mapCards.Length > 0)
            errors.Add("CharacterSelectScene must not use MapSelectionCard components.");

        CharacterSelectionPlayerPanel[] panels = GetSceneComponents<CharacterSelectionPlayerPanel>();
        if (panels.Length < 2)
            errors.Add("CharacterSelectScene should contain YOU and ENEMY player panels.");
        for (int i = 0; i < panels.Length; i++)
        {
            SerializedObject panel = new SerializedObject(panels[i]);
            RequireObject(panel, "perspectiveTitleText", errors);
            RequireObject(panel, "crownImage", errors);
            RequireObject(panel, "readyStateText", errors);
            RequireObject(panel, "characterInfoRoot", errors);
            RequireObject(panel, "characterPreviewRoot", errors);
            RequireObject(panel, "radarChartRoot", errors);
            RequireObject(panel, "previewController", errors);
            RequireObject(panel, "radarChartController", errors);
        }

        CharacterPreviewController[] previewControllers = GetSceneComponents<CharacterPreviewController>();
        for (int i = 0; i < previewControllers.Length; i++)
        {
            SerializedObject serializedPreview = new SerializedObject(previewControllers[i]);
            RequireObject(serializedPreview, "previewRoot", errors);
            RequireObject(serializedPreview, "portraitFallbackImage", errors);
            RequireObject(serializedPreview, "hiddenPortraitSprite", errors);
        }

        HexRadarChartGraphic[] charts = GetSceneComponents<HexRadarChartGraphic>();
        if (charts.Length < 2)
            errors.Add("CharacterSelectScene should contain radar charts for YOU and ENEMY panels.");

        for (int i = 0; i < charts.Length; i++)
        {
            Graphic[] graphics = charts[i].GetComponents<Graphic>();
            if (graphics.Length != 1)
                errors.Add("Radar chart object has extra Graphic components: " + charts[i].name);
        }

        CharacterRadarChartController[] radarControllers = GetSceneComponents<CharacterRadarChartController>();
        for (int i = 0; i < radarControllers.Length; i++)
        {
            SerializedObject serializedRadar = new SerializedObject(radarControllers[i]);
            RequireObject(serializedRadar, "animator", errors);
            RequireObjectArray(serializedRadar, "axisIcons", 6, errors);
        }

        TMP_Text[] texts = GetSceneComponents<TMP_Text>();
        for (int i = 0; i < texts.Length; i++)
        {
            string value = texts[i].text;
            if (ContainsForbiddenPlayerLabel(value))
                errors.Add("selection UI contains forbidden player label: " + texts[i].name + " = " + value);
        }
    }

    private static void ValidateGameScene(List<string> errors)
    {
        if (!LoadScene(GameScenePath, errors))
            return;

        SelectedBattleSetupLoader[] loaders = GetSceneComponents<SelectedBattleSetupLoader>();
        if (loaders.Length != 1)
        {
            errors.Add("GameScene must contain exactly one SelectedBattleSetupLoader.");
            return;
        }

        SerializedObject serializedLoader = new SerializedObject(loaders[0]);
        RequireObjectArray(serializedLoader, "characters", 2, errors);
        RequireObjectArray(serializedLoader, "maps", 1, errors);
        RequireObject(serializedLoader, "mapRoot", errors);
    }

    private static void ValidateBuildSettings(List<string> errors)
    {
        bool hasCharacterSelect = false;
        bool hasGameScene = false;

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path == CharacterSelectScenePath && scenes[i].enabled)
                hasCharacterSelect = true;
            if (scenes[i].path == GameScenePath && scenes[i].enabled)
                hasGameScene = true;
        }

        if (!hasCharacterSelect)
            errors.Add("CharacterSelectScene is missing from Build Settings.");
        if (!hasGameScene)
            errors.Add("GameScene is missing from Build Settings.");
    }

    private static bool LoadScene(string path, List<string> errors)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            errors.Add("could not open scene: " + path);
            return false;
        }

        return true;
    }

    private static T GetFirstSceneComponent<T>() where T : Component
    {
        T[] components = GetSceneComponents<T>();
        return components.Length > 0 ? components[0] : null;
    }

    private static T[] GetSceneComponents<T>() where T : Component
    {
        List<T> components = new List<T>();
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            components.AddRange(roots[i].GetComponentsInChildren<T>(true));

        return components.ToArray();
    }

    private static void RequireSceneAnimator(string objectName, List<string> errors)
    {
        GameObject obj = FindSceneObject(objectName);
        if (obj == null)
        {
            errors.Add("CharacterSelectScene missing object: " + objectName);
            return;
        }

        Animator animator = obj.GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
            errors.Add(objectName + " is missing generated Animator controller.");

        if (obj.GetComponent<CanvasGroup>() == null)
            errors.Add(objectName + " is missing CanvasGroup for generated alpha animations.");
    }

    private static GameObject FindSceneObject(string objectName)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChildRecursive(roots[i].transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void RequireObject(SerializedObject serializedObject, string propertyName, List<string> errors)
    {
        if (serializedObject == null || serializedObject.targetObject == null)
        {
            errors.Add("validation received a null SerializedObject for " + propertyName);
            return;
        }

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == null)
            errors.Add(serializedObject.targetObject.GetType().Name + " missing reference: " + propertyName);
    }

    private static void RequireObjectArray(SerializedObject serializedObject, string propertyName, int minimumCount, List<string> errors)
    {
        if (serializedObject == null || serializedObject.targetObject == null)
        {
            errors.Add("validation received a null SerializedObject for " + propertyName);
            return;
        }

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            errors.Add(serializedObject.targetObject.GetType().Name + " missing array: " + propertyName);
            return;
        }

        if (property.arraySize < minimumCount)
        {
            errors.Add(serializedObject.targetObject.GetType().Name + "." + propertyName + " must contain at least " + minimumCount + " entries.");
            return;
        }

        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == null)
                errors.Add(serializedObject.targetObject.GetType().Name + "." + propertyName + " has a null entry at index " + i + ".");
        }
    }

    private static bool ContainsForbiddenPlayerLabel(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.Contains("Player 1") ||
               value.Contains("Player 2") ||
               value.Contains("P1") ||
               value.Contains("P2") ||
               value.Contains("HOST") ||
               value.Contains("CLIENT") ||
               value.Contains("Host") ||
               value.Contains("Client") ||
               value.Contains("Photon") ||
               value.Contains("NickName") ||
               value.Contains("Nickname") ||
               value.Contains("Player Name");
    }

    private static bool HasTriggerParameter(AnimatorController controller, string triggerName)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].name == triggerName)
                return true;
        }

        return false;
    }

    private static bool HasAnyStateTransition(AnimatorController controller, string triggerName)
    {
        if (controller.layers.Length == 0 || controller.layers[0].stateMachine == null)
            return false;

        AnimatorStateTransition[] transitions = controller.layers[0].stateMachine.anyStateTransitions;
        for (int i = 0; i < transitions.Length; i++)
        {
            AnimatorCondition[] conditions = transitions[i].conditions;
            for (int j = 0; j < conditions.Length; j++)
            {
                if (conditions[j].mode == AnimatorConditionMode.If && conditions[j].parameter == triggerName)
                    return true;
            }
        }

        return false;
    }
}
