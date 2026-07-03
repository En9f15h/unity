using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ClassGameplayUILayout : MonoBehaviour
{
    [Header("Planning Layout")]
    [SerializeField] private RectTransform[] skillSpawnPoints;
    [SerializeField] private ActionDragSource[] skillSources;
    [SerializeField] private Transform masterSlotRoot;
    [SerializeField] private Transform clientSlotRoot;

    [Header("Planning UI")]
    [SerializeField] private Button readyButton;
    [SerializeField] private Text countdownText;
    [SerializeField] private Text debugText;
    [SerializeField] private RectTransform leftEnemyPreviewRoot;
    [SerializeField] private RectTransform rightEnemyPreviewRoot;
    [SerializeField] private GameObject previewCellPrefab;
    [SerializeField] private Sprite emptyPreviewSprite;

    [Header("Battle HUD")]
    [SerializeField] private Transform myEnergyRoot;
    [SerializeField] private EnergyBarUI enemyEnergyBar;
    [SerializeField] private DirectionalHealthBarUI clientHPBar;
    [SerializeField] private DirectionalHealthBarUI masterHPBar;

    public RectTransform[] SkillSpawnPoints => skillSpawnPoints;
    public ActionDragSource[] SkillSources => skillSources;
    public Transform MasterSlotRoot => masterSlotRoot;
    public Transform ClientSlotRoot => clientSlotRoot;
    public Button ReadyButton => readyButton;
    public Text CountdownText => countdownText;
    public Text DebugText => debugText;
    public RectTransform LeftEnemyPreviewRoot => leftEnemyPreviewRoot;
    public RectTransform RightEnemyPreviewRoot => rightEnemyPreviewRoot;
    public GameObject PreviewCellPrefab => previewCellPrefab;
    public Sprite EmptyPreviewSprite => emptyPreviewSprite;
    public DirectionalHealthBarUI ClientHPBar => clientHPBar;
    public DirectionalHealthBarUI MasterHPBar => masterHPBar;
    public Transform MyEnergyRoot => myEnergyRoot;
    public EnergyBarUI EnemyEnergyBar => enemyEnergyBar;

    private void Awake()
    {
        ResolveReferences();
    }

    public void ResolveReferences()
    {
        if (IsEmpty(skillSources))
            skillSources = GetComponentsInChildren<ActionDragSource>(true);

        if (IsEmpty(skillSpawnPoints))
            skillSpawnPoints = FindRectTransformsByName("skillspawn", "actionspawn", "skillpoint", "actionpoint");

        if (masterSlotRoot == null)
            masterSlotRoot = FindTransformByName("masterslotroot", "hostslotroot", "myslotroot");

        if (clientSlotRoot == null)
            clientSlotRoot = FindTransformByName("clientslotroot", "enemyslotroot", "opponentslotroot");

        if (readyButton == null)
            readyButton = FindComponentByName<Button>("ready");

        if (countdownText == null)
            countdownText = FindComponentByName<Text>("countdown", "timer", "time");

        if (debugText == null)
            debugText = FindComponentByName<Text>("debug", "status", "message");

        if (leftEnemyPreviewRoot == null)
            leftEnemyPreviewRoot = FindComponentByName<RectTransform>("leftenemypreview", "leftpreview");

        if (rightEnemyPreviewRoot == null)
            rightEnemyPreviewRoot = FindComponentByName<RectTransform>("rightenemypreview", "rightpreview");

        if (previewCellPrefab == null)
        {
            Transform previewCell = FindTransformByName("previewcellprefab", "enemypreviewcell", "previewcell");
            if (previewCell != null)
                previewCellPrefab = previewCell.gameObject;
        }

        if (myEnergyRoot == null)
            myEnergyRoot = FindTransformByName("myenergyroot", "playerenergyroot", "energyroot");

        if (enemyEnergyBar == null)
            enemyEnergyBar = FindComponentByName<EnergyBarUI>("enemyenergybar", "enemyenergy");

        if (masterHPBar == null)
            masterHPBar = FindComponentByName<DirectionalHealthBarUI>("masterhp", "hosthp", "lefthp");

        if (clientHPBar == null)
            clientHPBar = FindComponentByName<DirectionalHealthBarUI>("clienthp", "enemyhp", "righthp");
    }

    private bool IsEmpty<T>(T[] array)
    {
        return array == null || array.Length == 0;
    }

    private RectTransform[] FindRectTransformsByName(params string[] tokens)
    {
        List<RectTransform> results = new List<RectTransform>();
        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);

        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i] == null || rects[i].transform == transform)
                continue;

            string normalized = Normalize(rects[i].name);
            for (int j = 0; j < tokens.Length; j++)
            {
                if (normalized.Contains(tokens[j]))
                {
                    results.Add(rects[i]);
                    break;
                }
            }
        }

        return results.ToArray();
    }

    private Transform FindTransformByName(params string[] tokens)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] == null)
                continue;

            string normalized = Normalize(transforms[i].name);
            for (int j = 0; j < tokens.Length; j++)
            {
                if (normalized.Contains(tokens[j]))
                    return transforms[i];
            }
        }

        return null;
    }

    private T FindComponentByName<T>(params string[] tokens) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
                continue;

            string normalized = Normalize(components[i].name);
            for (int j = 0; j < tokens.Length; j++)
            {
                if (normalized.Contains(tokens[j]))
                    return components[i];
            }
        }

        return null;
    }

    private string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }
}
