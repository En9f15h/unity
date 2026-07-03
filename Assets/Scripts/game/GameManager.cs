using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [Header("Class Configs")]
    [SerializeField] private CharacterClassConfig[] classConfigs;

    [Header("Character Spawn Points")]
    [SerializeField] private Transform masterSpawnPoint;
    [SerializeField] private Transform clientSpawnPoint;

    [Header("Skill Spawn Points")]
    [SerializeField] private Transform[] skillSpawnPoints;

    [Header("Generated Slots")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform masterSlotRoot;
    [SerializeField] private Transform clientSlotRoot;

    [Header("Shared Actions")]
    [SerializeField] private ActionData moveForwardAction;
    [SerializeField] private ActionData moveBackwardAction;
    [SerializeField] private ActionData jumpAction;

    [Header("Class UI")]
    [SerializeField] private Transform classUiRoot;

    private int selectedClassIndex = 0;
    private int selectedSkinIndex = 0;

    private CharacterClassConfig currentConfig;
    private GameObject spawnedCharacter;
    private GameObject spawnedGameplayUi;
    private ClassGameplayUILayout currentLayout;
    private bool initialized;
    private Coroutine initializeRoutine;

    private readonly List<GameObject> spawnedSkillSources = new List<GameObject>();
    private readonly List<ActionDragSource> boundLayoutSkillSources = new List<ActionDragSource>();
    private readonly List<GameObject> spawnedSlots = new List<GameObject>();

    private void Start()
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            SceneManager.LoadScene("StartScene");
            return;
        }

        Debug.Log("GameManager Start");

        LoadPlayerSelection();
        LoadClassConfig();
        ApplyClassGameplayLayout();

        initializeRoutine = StartCoroutine(InitializeWhenSyncedStartReached());
    }

    private IEnumerator InitializeWhenSyncedStartReached()
    {
        Debug.Log("GameManager: waiting for GameSceneStartSync");

        while (GameSceneStartSync.Instance == null)
            yield return null;

        GameSceneStartSync.Instance.MarkLocalSceneReady();
        Debug.Log("GameManager: local GameScene load completed, waiting for remote player");

        while (!GameSceneStartSync.Instance.AreBothPlayersSceneReady())
            yield return null;

        Debug.Log("GameManager: both players loaded, waiting for synced start beat");

        while (!GameSceneStartSync.Instance.HasBeatStarted())
            yield return null;

        InitializeLocalGame();
        initializeRoutine = null;
    }

    private void OnDestroy()
    {
        if (initializeRoutine != null)
        {
            StopCoroutine(initializeRoutine);
            initializeRoutine = null;
        }

        ClearClassGameplayLayout();
    }

    private void InitializeLocalGame()
    {
        if (initialized)
            return;

        initialized = true;

        GenerateSlots();
        SpawnSkillSources();
        SpawnCharacterPrefab();

        Debug.Log("GameManager: synced start reached, local game objects spawned");
    }

    private ActionData[] GetCurrentActions()
    {
        if (currentConfig == null)
            return new ActionData[0];

        return new ActionData[]
        {
            currentConfig.lightAttack,
            currentConfig.heavyAttack,
            currentConfig.lowAttack,
            currentConfig.parry,
            currentConfig.defense,
            currentConfig.dance,
            currentConfig.ultimate,
            moveForwardAction,
            moveBackwardAction,
            jumpAction
        };
    }

    private Transform GetCurrentSlotRoot()
    {
        return PhotonNetwork.IsMasterClient ? masterSlotRoot : clientSlotRoot;
    }

    private void ApplyClassGameplayLayout()
    {
        ClearClassGameplayLayout();

        if (currentConfig == null || currentConfig.gameplayUiPrefab == null)
        {
            Debug.Log("GameManager: no class gameplay UI prefab assigned, using scene layout");
            return;
        }

        Transform parent = GetClassUiRoot();
        spawnedGameplayUi = Instantiate(currentConfig.gameplayUiPrefab, parent, false);

        ClassGameplayUILayout layout = spawnedGameplayUi.GetComponent<ClassGameplayUILayout>();
        if (layout == null)
            layout = spawnedGameplayUi.GetComponentInChildren<ClassGameplayUILayout>(true);

        if (layout == null)
        {
            Debug.LogWarning("GameManager: class gameplay UI prefab has no ClassGameplayUILayout -> " + spawnedGameplayUi.name);
            return;
        }

        layout.ResolveReferences();
        currentLayout = layout;
        ApplyLayoutReferences(layout);
        Debug.Log("GameManager: applied class gameplay UI layout -> " + spawnedGameplayUi.name);
    }

    private Transform GetClassUiRoot()
    {
        if (classUiRoot != null)
            return classUiRoot;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
            return canvas.transform;

        return transform;
    }

    private void ApplyLayoutReferences(ClassGameplayUILayout layout)
    {
        if (layout.SkillSpawnPoints != null && layout.SkillSpawnPoints.Length > 0)
            skillSpawnPoints = layout.SkillSpawnPoints;

        if (layout.MasterSlotRoot != null)
            masterSlotRoot = layout.MasterSlotRoot;

        if (layout.ClientSlotRoot != null)
            clientSlotRoot = layout.ClientSlotRoot;

        if (BattleUIManager.Instance != null)
            BattleUIManager.Instance.ApplyLayout(layout);

        if (TurnPlanningManager.Instance != null)
            TurnPlanningManager.Instance.ApplyLayout(layout);
    }

    private void ClearClassGameplayLayout()
    {
        if (spawnedGameplayUi != null)
        {
            Destroy(spawnedGameplayUi);
            spawnedGameplayUi = null;
        }

        currentLayout = null;
        boundLayoutSkillSources.Clear();
    }

    private void LoadPlayerSelection()
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("classIndex", out object classObj))
            selectedClassIndex = (int)classObj;

        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("skinIndex", out object skinObj))
            selectedSkinIndex = (int)skinObj;
    }

    private void LoadClassConfig()
    {
        if (classConfigs == null || classConfigs.Length == 0)
        {
            Debug.LogError("GameManager: classConfigs is not assigned");
            return;
        }

        if (selectedClassIndex < 0 || selectedClassIndex >= classConfigs.Length)
            selectedClassIndex = 0;

        currentConfig = classConfigs[selectedClassIndex];

        if (currentConfig == null)
            Debug.LogError("GameManager: currentConfig is null");
    }

    private void SpawnCharacterPrefab()
    {
        if (currentConfig == null)
        {
            Debug.LogError("SpawnCharacterPrefab: currentConfig is null");
            return;
        }

        Transform spawnPoint = PhotonNetwork.IsMasterClient ? masterSpawnPoint : clientSpawnPoint;

        if (spawnPoint == null)
        {
            Debug.LogError("SpawnCharacterPrefab: spawnPoint is not assigned");
            return;
        }

        if (currentConfig.skinPrefabs == null || currentConfig.skinPrefabs.Length == 0)
        {
            Debug.LogError($"SpawnCharacterPrefab: {currentConfig.className} has no skinPrefabs");
            return;
        }

        if (selectedSkinIndex < 0 || selectedSkinIndex >= currentConfig.skinPrefabs.Length)
            selectedSkinIndex = 0;

        GameObject prefab = currentConfig.skinPrefabs[selectedSkinIndex];

        if (prefab == null)
        {
            Debug.LogError("SpawnCharacterPrefab: prefab is null");
            return;
        }

        string resourcePath = BuildPhotonResourcePath(prefab);

        Debug.Log("Preparing Photon character spawn: " + resourcePath);

        GameObject obj = PhotonNetwork.Instantiate(
            resourcePath,
            spawnPoint.position,
            spawnPoint.rotation
        );

        if (obj == null)
        {
            Debug.LogError("SpawnCharacterPrefab: Photon spawn failed");
            return;
        }

        spawnedCharacter = obj;

        CharacterUnit unit = spawnedCharacter.GetComponent<CharacterUnit>();
        if (unit == null)
        {
            Debug.LogError("SpawnCharacterPrefab: CharacterUnit is missing on prefab");
            return;
        }

        string skinName = "Default";
        if (currentConfig.skinNames != null && selectedSkinIndex < currentConfig.skinNames.Length)
            skinName = currentConfig.skinNames[selectedSkinIndex];

        unit.Init(currentConfig.className, skinName, currentConfig.maxHP, currentConfig.slotCount);

        if (GameSceneStartSync.Instance != null)
            GameSceneStartSync.Instance.RegisterMyCharacter(spawnedCharacter);
    }

    private string BuildPhotonResourcePath(GameObject prefab)
    {
        // Character prefabs are expected at:
        // Assets/Resources/Prefab/<classFolder>/<prefab.name>.prefab
        // Example: Assets/Resources/Prefab/knight/knight_0.prefab
        string classFolder = currentConfig.photonResourceFolder;

        if (string.IsNullOrWhiteSpace(classFolder))
            classFolder = currentConfig.className.Replace(" ", "").ToLower();
        return "Prefab/" + classFolder + "/" + prefab.name;
    }

    private void SpawnSkillSources()
    {
        ClearSkillSources();

        if (currentConfig == null)
            return;

        ActionData[] actions = GetCurrentActions();
        if (TryBindLayoutSkillSources(actions))
            return;

        if (skillSpawnPoints == null)
        {
            Debug.LogWarning("SpawnSkillSources: skillSpawnPoints is not assigned");
            return;
        }

        int count = Mathf.Min(actions.Length, skillSpawnPoints.Length);

        for (int i = 0; i < count; i++)
        {
            if (actions[i] == null || actions[i].sourcePrefab == null || skillSpawnPoints[i] == null)
                continue;

            Transform spawnPoint = skillSpawnPoints[i];
            GameObject obj = Instantiate(actions[i].sourcePrefab, spawnPoint);

            RectTransform rect = obj.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
            }

            // The source button may keep ActionDragSource on a child.
            ActionDragSource drag = obj.GetComponent<ActionDragSource>();
            if (drag == null)
                drag = obj.GetComponentInChildren<ActionDragSource>(true);

            if (drag != null)
            {
                Canvas canvas = obj.GetComponentInParent<Canvas>();
                if (canvas != null)
                    drag.SetCanvas(canvas);

                // This binds the spawned source to its action data.
                drag.Init(actions[i]);

                Debug.Log($"SpawnSkillSources Init succeeded: {obj.name}, actionType={actions[i].actionType}, dataType={actions[i].GetType().Name}");
            }
            else
            {
                Debug.LogError($"SpawnSkillSources: {obj.name} is missing ActionDragSource");
            }

            spawnedSkillSources.Add(obj);
        }
    }

    private bool TryBindLayoutSkillSources(ActionData[] actions)
    {
        if (currentLayout == null || actions == null || actions.Length == 0)
            return false;

        currentLayout.ResolveReferences();
        ActionDragSource[] sources = currentLayout.SkillSources;
        if (sources == null || sources.Length == 0)
            return false;

        boundLayoutSkillSources.Clear();
        HashSet<ActionDragSource> usedSources = new HashSet<ActionDragSource>();
        Canvas canvas = currentLayout.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

        int boundCount = 0;
        for (int i = 0; i < actions.Length; i++)
        {
            ActionData action = actions[i];
            if (action == null)
                continue;

            ActionDragSource source = FindBestLayoutSkillSource(sources, action, usedSources);
            if (source == null)
                continue;

            if (canvas != null)
                source.SetCanvas(canvas);

            source.Init(action);
            usedSources.Add(source);
            boundLayoutSkillSources.Add(source);
            boundCount++;
        }

        if (boundCount > 0)
            Debug.Log("GameManager: bound existing layout skill sources, count = " + boundCount);

        return boundCount > 0;
    }

    private ActionDragSource FindBestLayoutSkillSource(ActionDragSource[] sources, ActionData action, HashSet<ActionDragSource> usedSources)
    {
        string actionName = NormalizeName(action.actionName);
        string prefabName = action.sourcePrefab != null ? NormalizeName(action.sourcePrefab.name) : string.Empty;

        for (int i = 0; i < sources.Length; i++)
        {
            ActionDragSource source = sources[i];
            if (source == null || usedSources.Contains(source))
                continue;

            string sourceName = NormalizeName(source.gameObject.name);
            if (MatchesActionSourceName(sourceName, actionName) || MatchesActionSourceName(sourceName, prefabName))
                return source;
        }

        for (int i = 0; i < sources.Length; i++)
        {
            ActionDragSource source = sources[i];
            if (source != null && !usedSources.Contains(source))
                return source;
        }

        return null;
    }

    private bool MatchesActionSourceName(string sourceName, string actionOrPrefabName)
    {
        if (string.IsNullOrEmpty(sourceName) || string.IsNullOrEmpty(actionOrPrefabName))
            return false;

        return sourceName == actionOrPrefabName ||
            sourceName.Contains(actionOrPrefabName) ||
            actionOrPrefabName.Contains(sourceName);
    }

    private string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private void GenerateSlots()
    {
        Transform slotRoot = GetCurrentSlotRoot();

        ClearSlots();

        if (currentConfig == null)
        {
            Debug.LogError("GenerateSlots: currentConfig is null");
            return;
        }

        if (slotPrefab == null)
        {
            Debug.LogError("GenerateSlots: slotPrefab is not assigned");
            return;
        }

        if (slotRoot == null)
        {
            Debug.LogError("GenerateSlots: slotRoot is not assigned");
            return;
        }

        for (int i = 0; i < currentConfig.slotCount; i++)
        {
            GameObject slot = Instantiate(slotPrefab);
            slot.name = "slot_" + i;
            slot.transform.SetParent(slotRoot, false);

            RectTransform rect = slot.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(100, 100);
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }

            spawnedSlots.Add(slot);
        }

        // Link slot order and nextSlot.
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            ActionSlot actionSlot = spawnedSlots[i].GetComponent<ActionSlot>();
            if (actionSlot != null)
            {
                actionSlot.SetIndex(i);

                ActionSlot next = null;
                if (i + 1 < spawnedSlots.Count)
                    next = spawnedSlots[i + 1].GetComponent<ActionSlot>();

                actionSlot.SetNextSlot(next);
            }
        }

        TurnPlanningManager planningManager = FindFirstObjectByType<TurnPlanningManager>();
        if (planningManager != null)
        {
            ActionSlot[] slots = slotRoot.GetComponentsInChildren<ActionSlot>();
            planningManager.SetPlanningSlots(slots);
        }

        Debug.Log("GenerateSlots completed, count = " + spawnedSlots.Count);
    }

    private void ClearSkillSources()
    {
        boundLayoutSkillSources.Clear();

        foreach (GameObject obj in spawnedSkillSources)
        {
            if (obj != null)
                Destroy(obj);
        }

        spawnedSkillSources.Clear();
    }

    private void ClearSlots()
    {
        Transform slotRoot = GetCurrentSlotRoot();
        if (slotRoot == null) return;

        for (int i = slotRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(slotRoot.GetChild(i).gameObject);
        }

        spawnedSlots.Clear();
    }
}
