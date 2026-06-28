using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [Header("戮穨戈")]
    [SerializeField] private CharacterClassConfig[] classConfigs;

    [Header("à︹ネΘ翴")]
    [SerializeField] private Transform masterSpawnPoint;
    [SerializeField] private Transform clientSpawnPoint;

    [Header("мネΘ翴")]
    [SerializeField] private Transform[] skillSpawnPoints;

    [Header("Slot 笆篈ネΘ")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform masterSlotRoot;
    [SerializeField] private Transform clientSlotRoot;

    [Header("┮Τ戮穨ノ︽笆")]
    [SerializeField] private ActionData moveForwardAction;
    [SerializeField] private ActionData moveBackwardAction;
    [SerializeField] private ActionData jumpAction;

    private int selectedClassIndex = 0;
    private int selectedSkinIndex = 0;

    private CharacterClassConfig currentConfig;
    private GameObject spawnedCharacter;

    private readonly List<GameObject> spawnedSkillSources = new List<GameObject>();
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

        GenerateSlots();         // ネΘ Slot
        SpawnSkillSources();     // ネΘмㄓ方
        SpawnCharacterPrefab();  // 程ネΘà︹
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
            Debug.LogError("GameManager: classConfigs ⊿Τ砞﹚");
            return;
        }

        if (selectedClassIndex < 0 || selectedClassIndex >= classConfigs.Length)
            selectedClassIndex = 0;

        currentConfig = classConfigs[selectedClassIndex];

        if (currentConfig == null)
            Debug.LogError("GameManager: currentConfig 琌");
    }

    private void SpawnCharacterPrefab()
    {
        if (currentConfig == null)
        {
            Debug.LogError("SpawnCharacterPrefab: currentConfig 琌");
            return;
        }

        Transform spawnPoint = PhotonNetwork.IsMasterClient ? masterSpawnPoint : clientSpawnPoint;

        if (spawnPoint == null)
        {
            Debug.LogError("SpawnCharacterPrefab: spawnPoint ⊿Τ砞﹚");
            return;
        }

        if (currentConfig.skinPrefabs == null || currentConfig.skinPrefabs.Length == 0)
        {
            Debug.LogError($"SpawnCharacterPrefab: {currentConfig.className} ⊿Τ skinPrefabs");
            return;
        }

        if (selectedSkinIndex < 0 || selectedSkinIndex >= currentConfig.skinPrefabs.Length)
            selectedSkinIndex = 0;

        GameObject prefab = currentConfig.skinPrefabs[selectedSkinIndex];

        if (prefab == null)
        {
            Debug.LogError("SpawnCharacterPrefab: prefab 琌");
            return;
        }

        string resourcePath = BuildPhotonResourcePath(prefab);

        Debug.Log("非称 Photon ネΘà︹: " + resourcePath);

        GameObject obj = PhotonNetwork.Instantiate(
            resourcePath,
            spawnPoint.position,
            spawnPoint.rotation
        );

        if (obj == null)
        {
            Debug.LogError("SpawnCharacterPrefab: Photon ネΘア毖");
            return;
        }

        spawnedCharacter = obj;

        CharacterUnit unit = spawnedCharacter.GetComponent<CharacterUnit>();
        if (unit == null)
        {
            Debug.LogError("SpawnCharacterPrefab: à︹ prefab ⊿Τ CharacterUnit");
            return;
        }

        string skinName = "Default";
        if (currentConfig.skinNames != null && selectedSkinIndex < currentConfig.skinNames.Length)
            skinName = currentConfig.skinNames[selectedSkinIndex];

        unit.Init(currentConfig.className, skinName, currentConfig.maxHP, currentConfig.slotCount);
        StartCoroutine(RegisterSpawnedCharacterToStartSync());
        if (GameSceneStartSync.Instance != null)
        {
            GameSceneStartSync.Instance.RegisterMyCharacter(spawnedCharacter);
        }
    }
    private IEnumerator RegisterSpawnedCharacterToStartSync()
    {
        while (spawnedCharacter == null)
            yield return null;

        while (GameSceneStartSync.Instance == null)
            yield return null;

        GameSceneStartSync.Instance.RegisterMyCharacter(spawnedCharacter);
    }
    private string BuildPhotonResourcePath(GameObject prefab)
    {
        // 硂柑安砞à︹ prefab 
        // Assets/Resources/Prefab/<className糶>/<prefab.name>.prefab
        // ㄒAssets/Resources/Prefab/knight/knight_0.prefab
        string classFolder = currentConfig.className.Replace(" ", "").ToLower();
        return "Prefab/" + classFolder + "/" + prefab.name;
    }

    private void SpawnSkillSources()
    {
        ClearSkillSources();

        if (currentConfig == null)
            return;

        ActionData[] actions = GetCurrentActions();
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

            // ㄓ方秙 ActionDragSource ぃ root璶┕ンт
            ActionDragSource drag = obj.GetComponent<ActionDragSource>();
            if (drag == null)
                drag = obj.GetComponentInChildren<ActionDragSource>(true);

            if (drag != null)
            {
                Canvas canvas = obj.GetComponentInParent<Canvas>();
                if (canvas != null)
                    drag.SetCanvas(canvas);

                // 硂︽程璶ぃ硂︽┷ㄓ方ッ环ぃ笵琌 Ultimate
                drag.Init(actions[i]);

                Debug.Log($"SpawnSkillSources Init Θ: {obj.name}, actionType={actions[i].actionType}, dataType={actions[i].GetType().Name}");
            }
            else
            {
                Debug.LogError($"SpawnSkillSources: {obj.name} тぃ ActionDragSource");
            }

            spawnedSkillSources.Add(obj);
        }
    }
    private void GenerateSlots()
    {
        Transform slotRoot = GetCurrentSlotRoot();

        ClearSlots();

        if (currentConfig == null)
        {
            Debug.LogError("GenerateSlots: currentConfig 琌");
            return;
        }

        if (slotPrefab == null)
        {
            Debug.LogError("GenerateSlots: slotPrefab ⊿Τ﹚");
            return;
        }

        if (slotRoot == null)
        {
            Debug.LogError("GenerateSlots: slotRoot ⊿Τ﹚");
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

        // 砞﹚腹籔 nextSlot
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

        TurnPlanningManager planningManager = FindObjectOfType<TurnPlanningManager>();
        if (planningManager != null)
        {
            ActionSlot[] slots = slotRoot.GetComponentsInChildren<ActionSlot>();
            planningManager.SetPlanningSlots(slots);
        }

        Debug.Log("GenerateSlots ЧΘ计秖 = " + spawnedSlots.Count);
    }

    private void ClearSkillSources()
    {
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