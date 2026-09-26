using UnityEngine;

public class DirectionalHealthBarUI : MonoBehaviour
{
    [SerializeField] private Transform fill;
    [SerializeField] private bool shrinkToLeft = false; // true = shrink left, false = shrink right
    [SerializeField] private GameObject readyIndicator;

    [Header("Perspective Label")]
    [SerializeField] private TextMesh perspectiveLabel;
    [SerializeField] private Vector3 perspectiveLabelLocalOffset = new Vector3(0f, -0.45f, -0.05f);
    [SerializeField] private int perspectiveLabelFontSize = 64;
    [SerializeField] private float perspectiveLabelCharacterSize = 0.045f;
    [SerializeField] private int perspectiveLabelSortingOrder = 30;
    [SerializeField] private Color youLabelColor = new Color(0.35f, 0.9f, 1f, 1f);
    [SerializeField] private Color enemyLabelColor = new Color(1f, 0.45f, 0.45f, 1f);
    [SerializeField] private Font perspectiveLabelFont;

    private float fullScaleX;
    private Vector3 fillStartLocalPos;
    private bool hasCachedFillStartState;
    private bool hasResolvedReferences;
    private bool createdPerspectiveLabel;
    private HealthBarPresentation presentation;
    private int displayedHP, displayedMaxHP;
    private string perspectiveCaption=string.Empty;
    private bool perspectiveIsLocal=true;

    private void Awake()
    {
        ResolveReferences();
        CacheFillStartState();
    }

    private void LateUpdate()
    {
        UpdatePerspectiveLabelTransform();
    }

    private void OnDestroy()
    {
        if (createdPerspectiveLabel && perspectiveLabel != null)
            Destroy(perspectiveLabel.gameObject);
    }

    public void ResolveReferences()
    {
        if (!hasResolvedReferences)
        {
            if (fill == null)
                fill = FindTransformByName("fill", "bar", "gauge");

            hasResolvedReferences = true;
        }

        if (perspectiveLabel == null)
            perspectiveLabel = FindComponentByName<TextMesh>("perspectivelabel", "ownerlabel", "playerlabel", "hplabel");

        if (readyIndicator == null)
        {
            Transform readyTransform = FindTransformByName("readyimage", "readyindicator", "readysprite");
            if (readyTransform != null)
                readyIndicator = readyTransform.gameObject;
        }
    }

    private void CacheFillStartState()
    {
        if (fill != null)
        {
            fullScaleX = fill.localScale.x;
            fillStartLocalPos = fill.localPosition;
            hasCachedFillStartState = true;
            presentation=GetComponent<HealthBarPresentation>() ?? gameObject.AddComponent<HealthBarPresentation>();
            presentation.Bind(fill.GetComponent<SpriteRenderer>(),fill.localScale,fillStartLocalPos,shrinkToLeft);
            presentation.SetPerspective(perspectiveIsLocal);
        }
    }

    public void Init(int currentHP, int maxHP)
    {
        SetHP(currentHP, maxHP);
        presentation?.SnapToCurrent();
    }

    public void SetHP(int currentHP, int maxHP)
    {
        ResolveReferences();

        if (fill == null || maxHP <= 0)
            return;

        if (!hasCachedFillStartState)
            CacheFillStartState();

        float ratio = Mathf.Clamp01((float)currentHP / maxHP);

        Vector3 scale = fill.localScale;
        scale.x = fullScaleX * ratio;
        fill.localScale = scale;

        // Keep the shrink direction fixed.
        float offset = (fullScaleX - scale.x) * 0.5f;

        Vector3 pos = fillStartLocalPos;
        if (shrinkToLeft)
            pos.x = fillStartLocalPos.x + offset;
        else
            pos.x = fillStartLocalPos.x - offset;

        fill.localPosition = pos;
        displayedHP=Mathf.Clamp(currentHP,0,maxHP); displayedMaxHP=maxHP;
        presentation?.SetHealth(currentHP,maxHP);
        RefreshLabelText();
    }

    public void SetPerspectiveLabel(string labelText, bool isLocalPlayer)
    {
        perspectiveIsLocal=isLocalPlayer;
        ResolveReferences();
        EnsurePerspectiveLabel();

        if (perspectiveLabel == null)
            return;

        bool hasText = !string.IsNullOrWhiteSpace(labelText);
        perspectiveLabel.gameObject.SetActive(hasText);

        if (!hasText)
            return;

        // 進入 GameScene 後，由 BattleUIManager 依照本機玩家身分設定 YOU / ENEMY。
        perspectiveCaption=labelText;
        RefreshLabelText();
        perspectiveLabel.color = isLocalPlayer ? youLabelColor : enemyLabelColor;
        presentation?.SetPerspective(isLocalPlayer);
        ConfigurePerspectiveLabel();
        UpdatePerspectiveLabelTransform();
    }

    private void RefreshLabelText()
    {
        if(perspectiveLabel==null) return;
        string value=displayedMaxHP>0 ? perspectiveCaption+"  "+displayedHP+" / "+displayedMaxHP : perspectiveCaption;
        if(perspectiveLabel.text!=value) perspectiveLabel.text=value;
    }

    public void SetReadyIndicatorActive(bool active)
    {
        ResolveReferences();

        if (readyIndicator != null)
            readyIndicator.SetActive(active);
    }

    private void EnsurePerspectiveLabel()
    {
        if (perspectiveLabel != null)
        {
            ConfigurePerspectiveLabel();
            return;
        }

        Transform labelParent = transform.parent != null ? transform.parent : transform;
        GameObject labelObject = new GameObject(gameObject.name + "_PerspectiveLabel");
        labelObject.transform.SetParent(labelParent, false);

        perspectiveLabel = labelObject.AddComponent<TextMesh>();
        createdPerspectiveLabel = true;
        ConfigurePerspectiveLabel();
    }

    private void ConfigurePerspectiveLabel()
    {
        if (perspectiveLabel == null)
            return;

        perspectiveLabel.anchor = TextAnchor.MiddleCenter;
        perspectiveLabel.alignment = TextAlignment.Center;
        perspectiveLabel.fontSize = perspectiveLabelFontSize;
        perspectiveLabel.characterSize = perspectiveLabelCharacterSize;
        perspectiveLabel.fontStyle = FontStyle.Bold;

        Font resolvedFont = ResolvePerspectiveLabelFont();
        if (resolvedFont != null)
            perspectiveLabel.font = resolvedFont;

        MeshRenderer meshRenderer = perspectiveLabel.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = perspectiveLabelSortingOrder;

            SpriteRenderer referenceRenderer = fill != null ? fill.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>(true);
            if (referenceRenderer != null)
                meshRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
        }
    }

    private Font ResolvePerspectiveLabelFont()
    {
        if (perspectiveLabelFont != null)
            return perspectiveLabelFont;

        perspectiveLabelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (perspectiveLabelFont == null)
            perspectiveLabelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        return perspectiveLabelFont;
    }

    private void UpdatePerspectiveLabelTransform()
    {
        if (perspectiveLabel == null || !perspectiveLabel.gameObject.activeSelf)
            return;

        Transform labelTransform = perspectiveLabel.transform;
        labelTransform.position = transform.TransformPoint(perspectiveLabelLocalOffset);

        // ClientHP 目前用旋轉鏡像血條；文字使用世界旋轉對齊鏡頭，避免 ENEMY/YOU 被鏡像反轉。
        Camera mainCamera = Camera.main;
        labelTransform.rotation = mainCamera != null ? mainCamera.transform.rotation : Quaternion.identity;
        labelTransform.localScale = Vector3.one;
    }

    private Transform FindTransformByName(params string[] tokens)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] == null || transforms[i] == transform)
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
            if (components[i] == null || components[i].transform == transform)
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
