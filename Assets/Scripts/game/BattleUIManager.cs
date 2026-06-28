using UnityEngine;

public class BattleUIManager : MonoBehaviour
{
    public static BattleUIManager Instance;

    [Header("HP Bar")]
    [SerializeField] private DirectionalHealthBarUI masterHPBar;
    [SerializeField] private DirectionalHealthBarUI clientHPBar;

    [Header("自己的能量條 Root（仍用 prefab 生成）")]
    [SerializeField] private Transform myEnergyRoot;

    [Header("場景中的敵人能量條（直接使用，不再生成 prefab）")]
    [SerializeField] private EnergyBarUI sceneEnemyEnergyBar;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public DirectionalHealthBarUI GetBarByOwner(bool ownerIsMaster)
    {
        return ownerIsMaster ? masterHPBar : clientHPBar;
    }

    public EnergyBarUI CreateMyEnergyBar(GameObject energyBarPrefab)
    {
        if (myEnergyRoot == null)
        {
            Debug.LogWarning("BattleUIManager: myEnergyRoot 沒有指定");
            return null;
        }

        if (energyBarPrefab == null)
        {
            Debug.LogWarning("BattleUIManager: energyBarPrefab 為空");
            return null;
        }

        ClearChildren(myEnergyRoot);

        GameObject obj = Instantiate(energyBarPrefab, myEnergyRoot, false);
        ResetUITransform(obj.transform);

        EnergyBarUI ui = obj.GetComponent<EnergyBarUI>();
        if (ui == null)
        {
            Debug.LogWarning("BattleUIManager: 自己的能量條 prefab 上沒有 EnergyBarUI -> " + obj.name);
            return null;
        }

        Debug.Log("成功建立自己的能量條: " + obj.name);
        return ui;
    }

    public EnergyBarUI CreateEnemyEnergyBar()
    {
        if (sceneEnemyEnergyBar == null)
        {
            Debug.LogWarning("BattleUIManager: sceneEnemyEnergyBar 沒有指定");
            return null;
        }

        sceneEnemyEnergyBar.gameObject.SetActive(true);

        RectTransform rt = sceneEnemyEnergyBar.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            // 不要再重設 anchoredPosition
            // rt.anchoredPosition = Vector2.zero;
        }

        Debug.Log("成功使用場景中的敵人能量條: " + sceneEnemyEnergyBar.name);
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
}