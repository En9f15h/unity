using Photon.Pun;
using UnityEngine;

public class BattleUIManager : MonoBehaviour
{
    public static BattleUIManager Instance;

    [Header("HP Bar")]
    [SerializeField] private DirectionalHealthBarUI masterHPBar;
    [SerializeField] private DirectionalHealthBarUI clientHPBar;

    [Header("My Energy Root")]
    [SerializeField] private Transform myEnergyRoot;

    [Header("Scene Enemy Energy Bar")]
    [SerializeField] private EnergyBarUI sceneEnemyEnergyBar;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveAssignedUI();
        RefreshPerspectiveLabels();
        if (GetComponent<ActionUsageMatchHUD>() == null) gameObject.AddComponent<ActionUsageMatchHUD>();
    }

    public DirectionalHealthBarUI GetBarByOwner(bool ownerIsMaster)
    {
        RefreshPerspectiveLabels();

        DirectionalHealthBarUI bar = ownerIsMaster ? masterHPBar : clientHPBar;
        if (bar != null)
            bar.ResolveReferences();

        return bar;
    }

    public void ApplyLayout(ClassGameplayUILayout layout)
    {
        if (layout == null)
            return;

        layout.ResolveReferences();

        if (layout.MasterHPBar != null)
            masterHPBar = layout.MasterHPBar;

        if (layout.ClientHPBar != null)
            clientHPBar = layout.ClientHPBar;

        if (layout.MyEnergyRoot != null)
            myEnergyRoot = layout.MyEnergyRoot;

        if (layout.EnemyEnergyBar != null)
            sceneEnemyEnergyBar = layout.EnemyEnergyBar;

        ResolveAssignedUI();
        RefreshPerspectiveLabels();
    }

    public void RefreshPerspectiveLabels()
    {
        bool localPlayerIsMaster = IsLocalPlayerMaster();

        // MasterHP / ClientHP 沿用現有 ownerIsMaster 對應，不改變血量同步或戰鬥邏輯。
        if (masterHPBar != null)
            masterHPBar.SetPerspectiveLabel(localPlayerIsMaster ? "YOU" : "ENEMY", localPlayerIsMaster);

        if (clientHPBar != null)
            clientHPBar.SetPerspectiveLabel(localPlayerIsMaster ? "ENEMY" : "YOU", !localPlayerIsMaster);
    }

    public void SetReadyIndicatorByOwner(bool ownerIsMaster, bool active)
    {
        DirectionalHealthBarUI bar = ownerIsMaster ? masterHPBar : clientHPBar;
        if (bar == null)
            return;

        bar.ResolveReferences();
        bar.SetReadyIndicatorActive(active);
    }

    public void SetReadyIndicators(bool masterReady, bool clientReady)
    {
        SetReadyIndicatorByOwner(true, masterReady);
        SetReadyIndicatorByOwner(false, clientReady);
    }

    public void HideAllReadyIndicators()
    {
        SetReadyIndicators(false, false);
    }

    public EnergyBarUI CreateMyEnergyBar(GameObject energyBarPrefab)
    {
        if (myEnergyRoot == null)
        {
            Debug.LogWarning("BattleUIManager: myEnergyRoot is not assigned");
            return null;
        }

        EnergyBarUI existingUi = myEnergyRoot.GetComponent<EnergyBarUI>();
        if (existingUi != null)
        {
            existingUi.gameObject.SetActive(true);
            existingUi.ResolveReferences();
            Debug.Log("Using layout-authored my energy bar: " + existingUi.name);
            return existingUi;
        }

        if (energyBarPrefab == null)
        {
            Debug.LogWarning("BattleUIManager: energyBarPrefab is null");
            return null;
        }

        ClearChildren(myEnergyRoot);

        GameObject obj = Instantiate(energyBarPrefab, myEnergyRoot, false);
        ResetUITransform(obj.transform);

        EnergyBarUI ui = obj.GetComponent<EnergyBarUI>();
        if (ui == null)
        {
            Debug.LogWarning("BattleUIManager: EnergyBarUI is missing on my energy prefab -> " + obj.name);
            return null;
        }

        ui.ResolveReferences();
        Debug.Log("Created my energy bar: " + obj.name);
        return ui;
    }

    public EnergyBarUI CreateEnemyEnergyBar()
    {
        if (sceneEnemyEnergyBar == null)
        {
            Debug.LogWarning("BattleUIManager: sceneEnemyEnergyBar is not assigned");
            return null;
        }

        sceneEnemyEnergyBar.gameObject.SetActive(true);
        sceneEnemyEnergyBar.ResolveReferences();

        RectTransform rt = sceneEnemyEnergyBar.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            // Keep the layout-authored anchoredPosition.
        }

        Debug.Log("Using scene enemy energy bar: " + sceneEnemyEnergyBar.name);
        return sceneEnemyEnergyBar;
    }

    private void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void ResetUITransform(Transform t)
    {
        t.localScale = Vector3.one;
        t.localRotation = Quaternion.identity;
        t.localPosition = Vector3.zero;

        RectTransform rt = t as RectTransform;
        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
        }
    }

    private void ResolveAssignedUI()
    {
        if (masterHPBar != null)
            masterHPBar.ResolveReferences();

        if (clientHPBar != null)
            clientHPBar.ResolveReferences();

        if (sceneEnemyEnergyBar != null)
            sceneEnemyEnergyBar.ResolveReferences();
    }

    private bool IsLocalPlayerMaster()
    {
        if (PhotonNetwork.LocalPlayer != null)
            return PhotonNetwork.LocalPlayer.IsMasterClient;

        // Editor 單機測試沒有 Photon LocalPlayer 時，預設左側 MasterHP 是自己。
        return true;
    }
}
