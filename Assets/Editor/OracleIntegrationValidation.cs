using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OracleIntegrationValidation
{
    private const string OracleConfigPath = "Assets/Scripts/game/classes/OracleConfig.asset";
    private const string OracleGameplayUiPath = "Assets/Resources/Prefab/Oracle/UI/OracleGameplayUI.prefab";
    private const string OracleActionPrefabFolder = "Assets/Resources/Prefab/Oracle/UI/Actions";
    private const string SharedDancePrefabPath = "Assets/Resources/Prefab/Dance.prefab";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string RoomScenePath = "Assets/Scenes/RoomScene.unity";

    private static readonly List<string> failures = new List<string>();

    [MenuItem("Tools/Validation/Oracle Integration")]
    public static void RunFromMenu()
    {
        RunValidation(exitOnComplete: false);
    }

    public static void RunFromCommandLine()
    {
        RunValidation(exitOnComplete: true);
    }

    private static void RunValidation(bool exitOnComplete)
    {
        failures.Clear();

        ValidateOracleConfig();
        ValidateGameScene();
        ValidateRoomScene();

        if (failures.Count > 0)
        {
            for (int i = 0; i < failures.Count; i++)
                Debug.LogError("[OracleValidation] " + failures[i]);

            if (exitOnComplete)
                EditorApplication.Exit(1);

            return;
        }

        Debug.Log("[OracleValidation] Oracle integration validation passed.");

        if (exitOnComplete)
            EditorApplication.Exit(0);
    }

    private static void ValidateOracleConfig()
    {
        OracleClassConfig config = AssetDatabase.LoadAssetAtPath<OracleClassConfig>(OracleConfigPath);
        Require(config != null, "OracleConfig.asset is missing or is not an OracleClassConfig.");

        if (config == null)
            return;

        Require(config.className == "Oracle", "Oracle className must be Oracle.");
        Require(config.photonResourceFolder == "Oracle", "Oracle photonResourceFolder must be Oracle.");
        Require(config.maxHP == 25, "Oracle maxHP must be 25.");
        Require(config.slotCount == 5, "Oracle slotCount must be 5.");
        Require(config.energyBarPrefab != null, "Oracle energyBarPrefab is missing.");
        Require(config.gameplayUiPrefab != null, "Oracle gameplayUiPrefab is missing.");
        Require(config.skinPrefabs != null && config.skinPrefabs.Length > 0 && config.skinPrefabs[0] != null, "Oracle needs at least one skin prefab.");

        if (config.skinPrefabs != null && config.skinPrefabs.Length > 0 && config.skinPrefabs[0] != null)
        {
            string folder = string.IsNullOrWhiteSpace(config.photonResourceFolder)
                ? config.className.ToLowerInvariant()
                : config.photonResourceFolder;
            string resourcePath = "Prefab/" + folder + "/" + config.skinPrefabs[0].name;
            Require(Resources.Load<GameObject>(resourcePath) != null, "Oracle Photon prefab is not loadable from Resources: " + resourcePath);
        }

        ValidateAttack(config.bolt, ActionType.Bolt, "Bolt", 1, 3, 2, 4);
        ValidateAttack(config.rift, ActionType.Rift, "Rift", 2, 4, 2, 3);
        ValidateAction(config.shift, ActionType.Shift, "Shift", 2);
        ValidateAction(config.ward, ActionType.Ward, "Ward", 1);
        ValidateAction(config.fade, ActionType.Fade, "Fade", 2);
        ValidateUltimate(config.sight, ActionType.Sight, "Sight", 1, 0);
        ValidateDance(config.dance);
        ValidateInheritedBaseActionsCleared(config);

        Require(config.GetActionData(ActionType.Bolt) == config.bolt, "Oracle GetActionData(Bolt) must return bolt.");
        Require(config.GetActionData(ActionType.Rift) == config.rift, "Oracle GetActionData(Rift) must return rift.");
        Require(config.GetActionData(ActionType.Shift) == config.shift, "Oracle GetActionData(Shift) must return shift.");
        Require(config.GetActionData(ActionType.Ward) == config.ward, "Oracle GetActionData(Ward) must return ward.");
        Require(config.GetActionData(ActionType.Fade) == config.fade, "Oracle GetActionData(Fade) must return fade.");
        Require(config.GetActionData(ActionType.Sight) == config.sight, "Oracle GetActionData(Sight) must return sight.");
        Require(config.GetActionData(ActionType.Ultimate) == config.sight, "Oracle GetActionData(Ultimate) must return sight.");
        Require(config.GetActionData(ActionType.Dance) == config.dance, "Oracle GetActionData(Dance) must return dance.");

        ActionData[] actions = config.GetClassActions();
        Require(actions != null && actions.Length == 7, "Oracle GetClassActions must return 6 Oracle skills plus Dance.");

        if (config.shiftCooldownOverlay != null && config.shift != null)
            Require(config.shift.cooldownOverlaySprite == config.shiftCooldownOverlay, "Shift cooldown overlay must match Oracle shiftCooldownOverlay when the overlay is assigned.");

        if (config.shiftCooldownOverlay != null && config.fade != null)
            Require(config.fade.cooldownOverlaySprite == config.shiftCooldownOverlay, "Fade cooldown overlay must match Oracle shiftCooldownOverlay when the overlay is assigned.");

        if (config.sightUsedOverlay != null && config.sight != null)
            Require(config.sight.usedOverlaySprite == config.sightUsedOverlay, "Sight used overlay must match Oracle sightUsedOverlay when the overlay is assigned.");

        if (config.riftSecondarySlotLockedIcon != null && config.rift != null)
            Require(config.rift.lockedContinuationSprite == config.riftSecondarySlotLockedIcon, "Rift continuation sprite must match Oracle riftSecondarySlotLockedIcon when the icon is assigned.");

        ValidateOracleUi(config);
    }

    private static void ValidateAttack(AttackActionData action, ActionType actionType, string name, int slotCost, int damage, int minRange, int range)
    {
        ValidateAction(action, actionType, name, slotCost);

        if (action == null)
            return;

        Require(action.damage == damage, name + " damage must be " + damage + ".");
        Require(action.minRange == minRange, name + " minRange must be " + minRange + ".");
        Require(action.range == range, name + " range must be " + range + ".");
    }

    private static void ValidateUltimate(UltimateActionData action, ActionType actionType, string name, int slotCost, int damage)
    {
        ValidateAction(action, actionType, name, slotCost);

        if (action == null)
            return;

        Require(action.damage == damage, name + " damage must be " + damage + ".");
    }

    private static void ValidateAction(ActionData action, ActionType actionType, string name, int slotCost)
    {
        Require(action != null, name + " action data is missing.");

        if (action == null)
            return;

        Require(action.actionName == name, name + " actionName must be " + name + ".");
        Require(action.actionType == actionType, name + " actionType is incorrect.");
        Require(action.GetSlotCost() == slotCost, name + " slotCost must be " + slotCost + ".");
        Require(action.sourcePrefab != null, name + " sourcePrefab is missing.");
        Require(action.iconSprite != null, name + " iconSprite is missing.");
        Require(!string.IsNullOrEmpty(action.tooltipTitle), name + " tooltipTitle is missing.");

        if (action.sourcePrefab != null)
        {
            string path = AssetDatabase.GetAssetPath(action.sourcePrefab);
            string expectedPath = OracleActionPrefabFolder + "/Oracle_" + name + ".prefab";
            Require(path == expectedPath, name + " sourcePrefab must be " + expectedPath + ".");

            ActionDragSource source = action.sourcePrefab.GetComponent<ActionDragSource>();
            if (source == null)
                source = action.sourcePrefab.GetComponentInChildren<ActionDragSource>(true);

            Require(source != null, name + " sourcePrefab must contain ActionDragSource.");
        }
    }

    private static void ValidateDance(DanceActionData action)
    {
        Require(action != null, "Dance action data is missing.");

        if (action == null)
            return;

        Require(action.actionName == "Dance", "Dance actionName must be Dance.");
        Require(action.actionType == ActionType.Dance, "Dance actionType is incorrect.");
        Require(action.GetSlotCost() == 1, "Dance slotCost must be 1.");
        Require(action.energyGain == 1, "Dance energyGain must be 1.");
        Require(action.sourcePrefab != null, "Dance sourcePrefab is missing.");
        Require(action.iconSprite != null, "Dance iconSprite is missing.");
        Require(!string.IsNullOrEmpty(action.tooltipTitle), "Dance tooltipTitle is missing.");

        if (action.sourcePrefab != null)
        {
            string path = AssetDatabase.GetAssetPath(action.sourcePrefab);
            Require(path == SharedDancePrefabPath, "Dance sourcePrefab must be " + SharedDancePrefabPath + ".");
        }
    }

    private static void ValidateInheritedBaseActionsCleared(OracleClassConfig config)
    {
        RequireBaseActionCleared(config.lightAttack, "lightAttack");
        RequireBaseActionCleared(config.heavyAttack, "heavyAttack");
        RequireBaseActionCleared(config.lowAttack, "lowAttack");
        RequireBaseActionCleared(config.parry, "parry");
        RequireBaseActionCleared(config.defense, "defense");
        RequireBaseActionCleared(config.ultimate, "ultimate");
    }

    private static void RequireBaseActionCleared(ActionData action, string fieldName)
    {
        if (action == null)
            return;

        Require(action.actionType == ActionType.None, "Oracle inherited " + fieldName + " must be cleared to ActionType.None.");
        Require(action.sourcePrefab == null, "Oracle inherited " + fieldName + " sourcePrefab must be cleared.");
        Require(action.iconSprite == null, "Oracle inherited " + fieldName + " iconSprite must be cleared.");
    }

    private static void ValidateOracleUi(OracleClassConfig config)
    {
        GameObject gameplayUi = AssetDatabase.LoadAssetAtPath<GameObject>(OracleGameplayUiPath);
        Require(gameplayUi != null, "OracleGameplayUI prefab is missing.");

        if (config != null && gameplayUi != null)
            Require(config.gameplayUiPrefab == gameplayUi, "Oracle gameplayUiPrefab must point to OracleGameplayUI.prefab.");

        if (gameplayUi == null)
            return;

        ClassGameplayUILayout layout = gameplayUi.GetComponent<ClassGameplayUILayout>();
        if (layout == null)
            layout = gameplayUi.GetComponentInChildren<ClassGameplayUILayout>(true);

        Require(layout != null, "OracleGameplayUI must contain ClassGameplayUILayout.");

        ActionDragSource[] sources = gameplayUi.GetComponentsInChildren<ActionDragSource>(true);
        Require(sources != null && sources.Length >= 10, "OracleGameplayUI must contain the 6 Oracle skills plus Dance, Move Forward, Move Backward, and Jump.");

        RequireContainsLayoutSource(sources, ActionType.Bolt, "Bolt");
        RequireContainsLayoutSource(sources, ActionType.Rift, "Rift");
        RequireContainsLayoutSource(sources, ActionType.Shift, "Shift");
        RequireContainsLayoutSource(sources, ActionType.Ward, "Ward");
        RequireContainsLayoutSource(sources, ActionType.Fade, "Fade");
        RequireContainsLayoutSource(sources, ActionType.Sight, "Sight");
        RequireContainsLayoutSource(sources, ActionType.Dance, "Dance");
        RequireContainsLayoutSource(sources, ActionType.MoveForward, "Move Forward");
        RequireContainsLayoutSource(sources, ActionType.MoveBackward, "Move Backward");
        RequireContainsLayoutSource(sources, ActionType.Jump, "Jump");
    }

    private static void RequireContainsLayoutSource(ActionDragSource[] sources, ActionType actionType, string label)
    {
        bool found = false;
        if (sources != null)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                ActionDragSource source = sources[i];
                if (source == null)
                    continue;

                if (source.GetActionType() == actionType)
                {
                    found = true;
                    break;
                }

                ActionDragData dragData = source.GetComponent<ActionDragData>();
                if (dragData == null)
                    dragData = source.GetComponentInChildren<ActionDragData>(true);

                if (dragData != null && dragData.actionType == actionType)
                {
                    found = true;
                    break;
                }
            }
        }

        Require(found, "OracleGameplayUI must contain an ActionDragSource for " + label + ".");
    }

    private static void ValidateGameScene()
    {
        Require(System.IO.File.Exists(GameScenePath), "GameScene is missing at " + GameScenePath + ".");

        if (!System.IO.File.Exists(GameScenePath))
            return;

        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        ValidateNoMissingScripts(SceneManager.GetActiveScene(), "GameScene");

        GameManager gameManager = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        TurnPlanningManager turnPlanningManager = Object.FindFirstObjectByType<TurnPlanningManager>(FindObjectsInactive.Include);

        Require(gameManager != null, "GameScene is missing GameManager.");
        Require(turnPlanningManager != null, "GameScene is missing TurnPlanningManager.");

        if (gameManager != null)
            RequireSerializedClassConfigContainsOracle(gameManager, "GameManager");

        if (turnPlanningManager != null)
            RequireSerializedClassConfigContainsOracle(turnPlanningManager, "TurnPlanningManager");
    }

    private static void ValidateRoomScene()
    {
        Require(System.IO.File.Exists(RoomScenePath), "RoomScene is missing at " + RoomScenePath + ".");

        if (!System.IO.File.Exists(RoomScenePath))
            return;

        EditorSceneManager.OpenScene(RoomScenePath, OpenSceneMode.Single);
        ValidateNoMissingScripts(SceneManager.GetActiveScene(), "RoomScene");

        RoomManager roomManager = Object.FindFirstObjectByType<RoomManager>(FindObjectsInactive.Include);
        Require(roomManager != null, "RoomScene is missing RoomManager.");

        if (roomManager == null)
            return;

        SerializedObject serializedObject = new SerializedObject(roomManager);
        SerializedProperty classNames = serializedObject.FindProperty("classNames");
        Require(StringArrayContains(classNames, "Oracle"), "RoomManager classNames must contain Oracle.");

        SerializedProperty oracleSkins = serializedObject.FindProperty("oracleSkins");
        Require(oracleSkins != null && oracleSkins.isArray && oracleSkins.arraySize > 0, "RoomManager oracleSkins must contain at least one skin.");

        SerializedProperty oracleSkinSprites = serializedObject.FindProperty("oracleSkinSprites");
        Require(oracleSkinSprites != null && oracleSkinSprites.isArray && oracleSkinSprites.arraySize > 0, "RoomManager oracleSkinSprites must contain at least one sprite.");
    }

    private static void RequireSerializedClassConfigContainsOracle(Object target, string label)
    {
        OracleClassConfig oracleConfig = AssetDatabase.LoadAssetAtPath<OracleClassConfig>(OracleConfigPath);
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty classConfigs = serializedObject.FindProperty("classConfigs");

        Require(classConfigs != null && classConfigs.isArray, label + " classConfigs is missing.");

        if (classConfigs == null || !classConfigs.isArray)
            return;

        bool found = false;
        for (int i = 0; i < classConfigs.arraySize; i++)
        {
            SerializedProperty item = classConfigs.GetArrayElementAtIndex(i);
            if (item.objectReferenceValue == oracleConfig)
            {
                found = true;
                break;
            }
        }

        Require(found, label + " classConfigs must include OracleConfig.asset.");
    }

    private static void ValidateNoMissingScripts(Scene scene, string sceneLabel)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            ValidateNoMissingScriptsRecursive(roots[i].transform, sceneLabel);
    }

    private static void ValidateNoMissingScriptsRecursive(Transform transform, string sceneLabel)
    {
        Component[] components = transform.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Require(components[i] != null, sceneLabel + " has a missing script on object: " + GetPath(transform));
        }

        for (int i = 0; i < transform.childCount; i++)
            ValidateNoMissingScriptsRecursive(transform.GetChild(i), sceneLabel);
    }

    private static bool StringArrayContains(SerializedProperty property, string value)
    {
        if (property == null || !property.isArray)
            return false;

        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty item = property.GetArrayElementAtIndex(i);
            if (item != null && item.stringValue == value)
                return true;
        }

        return false;
    }

    private static string GetPath(Transform transform)
    {
        if (transform == null)
            return "<null>";

        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            failures.Add(message);
    }
}
