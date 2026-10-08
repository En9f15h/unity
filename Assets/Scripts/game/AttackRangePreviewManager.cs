using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum AttackRangeDisplayType
{
    My,
    Enemy
}

[DisallowMultipleComponent]
public class AttackRangePreviewManager : MonoBehaviour
{
    [Header("Display State")]
    [SerializeField, Tooltip("Local-only setting. Shows attack ranges for the local player's character.")]
    private bool showMyRange = true;

    [SerializeField, Tooltip("Local-only setting. Shows attack ranges for the remote opponent.")]
    private bool showEnemyRange = true;

    [SerializeField, HideInInspector]
    private bool showAttackRange = true;

    [Header("My Range Visual")]
    [SerializeField, Tooltip("Transparent green fill color used by local-player attack-range tiles.")]
    private Color myFillColor = new Color(0f, 1f, 0.16f, 0.22f);

    [SerializeField, Tooltip("Bright green border color used by local-player attack-range tiles.")]
    private Color myBorderColor = new Color(0.35f, 1f, 0.08f, 0.92f);

    [Header("Enemy Range Visual")]
    [SerializeField, Tooltip("Transparent red fill color used by enemy attack-range tiles.")]
    private Color fillColor = new Color(1f, 0f, 0f, 0.22f);

    [SerializeField, Tooltip("Bright red border color used by enemy attack-range tiles.")]
    private Color borderColor = new Color(1f, 0.16f, 0.04f, 0.88f);

    [SerializeField, Range(0.85f, 1.05f), Tooltip("Fill scale relative to one full tile.")]
    private float fillScale = 0.96f;

    [SerializeField, Range(1f, 1.12f), Tooltip("Glow border scale relative to one full tile.")]
    private float borderScale = 1.03f;

    [Header("Tile Scale")]
    [SerializeField, Min(0.01f), Tooltip("World-space width of one logical tile.")]
    private float tileWidth = 1f;

    [SerializeField, Min(0.01f), Tooltip("World-space height of one visual tile.")]
    private float tileHeight = 1f;

    [SerializeField, Tooltip("World offset added to every generated range cell.")]
    private Vector3 worldOffset = Vector3.zero;

    [Header("Sorting")]
    [SerializeField, Tooltip("Uses the attacker's SpriteRenderer sorting layer when possible.")]
    private bool useAttackerSortingLayer = true;

    [SerializeField, Tooltip("Fallback sorting layer when no attacker SpriteRenderer is available.")]
    private string sortingLayerName = "Default";

    [SerializeField, Tooltip("Fallback sorting order when no attacker SpriteRenderer is available.")]
    private int sortingOrder = 30;

    [SerializeField, Tooltip("Sorting order added above the attacker's SpriteRenderer sorting order.")]
    private int attackerSortingOrderOffset = 20;

    [Header("Timing")]
    [SerializeField, Min(0.01f), Tooltip("Fallback visible duration in unscaled seconds.")]
    private float defaultDuration = 0.5f;

    [SerializeField, Min(0.01f), Tooltip("Fade duration after the hit beat. This does not delay combat.")]
    private float fadeDuration = 0.12f;

    [Header("Pulse")]
    [SerializeField, Tooltip("Subtly pulses the border during the warning window.")]
    private bool pulseBorder = true;

    [SerializeField, Range(1f, 1.08f), Tooltip("Maximum pulse scale for the border.")]
    private float pulseScale = 1.03f;

    [SerializeField, Range(0.1f, 1f), Tooltip("Minimum alpha multiplier used by the border pulse.")]
    private float pulseMinAlpha = 0.72f;

    [Header("Canvas Toggle")]
    [SerializeField, Tooltip("Creates local Canvas buttons named MyRangeToggleButton and EnemyRangeToggleButton when no buttons are assigned.")]
    private bool autoCreateCanvasToggle = true;

    [SerializeField] private Button myRangeToggleButton;
    [SerializeField] private Text myRangeToggleLabel;
    [SerializeField] private Image myRangeToggleImage;
    [SerializeField] private Button enemyRangeToggleButton;
    [SerializeField] private Text enemyRangeToggleLabel;
    [SerializeField] private Image enemyRangeToggleImage;
    [SerializeField, HideInInspector] private Button rangeToggleButton;
    [SerializeField, HideInInspector] private Text rangeToggleLabel;
    [SerializeField, HideInInspector] private Image rangeToggleImage;
    [SerializeField] private Vector2 toggleAnchoredPosition = new Vector2(0f, -72f);
    [SerializeField] private Vector2 toggleSize = new Vector2(92f, 34f);

    [Header("Hierarchy")]
    [SerializeField, Tooltip("Optional root used to organize generated range preview cells.")]
    private Transform previewRoot;

    private static Sprite generatedFillSprite;
    private static Sprite generatedBorderSprite;
    private static Texture2D generatedFillTexture;
    private static Texture2D generatedBorderTexture;

    private readonly List<GameObject> myRangeGroups = new List<GameObject>();
    private readonly List<GameObject> enemyRangeGroups = new List<GameObject>();
    private GameObject hoverGroup;
    private AttackRangeDisplayType hoverDisplayType;
    private Coroutine hoverPulseCoroutine;
    private const string TimedGroupName = "AttackRangePreviewGroup";
    private const string HoverGroupName = "AttackRangeHoverPreviewGroup";
    private Material rangePresentationMaterial;
    private MaterialPropertyBlock presentationBlock;
    private static readonly int RangeLayerId = Shader.PropertyToID("_Layer");
    private static readonly int EnemyStyleId = Shader.PropertyToID("_EnemyStyle");
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");

    public bool ShowAttackRange => showMyRange || showEnemyRange;
    public bool ShowMyRange => showMyRange;
    public bool ShowEnemyRange => showEnemyRange;

    private void Awake()
    {
        SyncLegacyVisibilityFlag();
        ResolvePreviewRoot();
        EnsureSprites();
        EnsureCanvasToggles();
        UpdateToggleVisual();
    }

    private void Start()
    {
        EnsureCanvasToggles();
        UpdateToggleVisual();
        
    }

    private void OnDisable()
    {
        ClearAll();
    }

    public void SetTileSize(float worldUnitsPerTile)
    {
        SetTileSize(worldUnitsPerTile, worldUnitsPerTile);
    }

    public void SetTileSize(float width, float height)
    {
        tileWidth = Mathf.Max(0.01f, width);
        tileHeight = Mathf.Max(0.01f, height);
    }

    public void SetVisibleEnabled(bool enabled)
    {
        showMyRange = enabled;
        showEnemyRange = enabled;
        showAttackRange = enabled;
        if (!ShowAttackRange)
            ClearAll();

        UpdateToggleVisual();
    }

    public void ToggleVisibleEnabled()
    {
        SetVisibleEnabled(!ShowAttackRange);
    }

    public void SetMyRangeEnabled(bool enabled)
    {
        showMyRange = enabled;
        showAttackRange = ShowAttackRange;
        if (!showMyRange)
        {
            ClearRangeGroups(myRangeGroups);
            if (hoverGroup != null && hoverDisplayType == AttackRangeDisplayType.My)
                ClearHoverRange();
        }

        UpdateToggleVisual();
    }

    public void SetEnemyRangeEnabled(bool enabled)
    {
        showEnemyRange = enabled;
        showAttackRange = ShowAttackRange;
        if (!showEnemyRange)
        {
            ClearRangeGroups(enemyRangeGroups);
            if (hoverGroup != null && hoverDisplayType == AttackRangeDisplayType.Enemy)
                ClearHoverRange();
        }

        UpdateToggleVisual();
    }

    public void ToggleMyRangeEnabled()
    {
        SetMyRangeEnabled(!showMyRange);
    }

    public void ToggleEnemyRangeEnabled()
    {
        SetEnemyRangeEnabled(!showEnemyRange);
    }

    public bool CanShowRangeFor(CharacterUnit attacker)
    {
        return attacker != null && IsRangeTypeVisible(GetDisplayType(attacker));
    }

    public void PositionToggleBelow(RectTransform anchorToggle, float verticalSpacing)
    {
        if (anchorToggle == null)
            return;

        EnsureCanvasToggles();

        RectTransform myRect = myRangeToggleButton != null ? myRangeToggleButton.transform as RectTransform : null;
        RectTransform enemyRect = enemyRangeToggleButton != null ? enemyRangeToggleButton.transform as RectTransform : null;
        if (myRect == null && enemyRect == null)
            return;

        PositionButtonBelow(myRect, anchorToggle, verticalSpacing);
        if (myRect != null)
            PositionButtonBelow(enemyRect, myRect, verticalSpacing);
        else
            PositionButtonBelow(enemyRect, anchorToggle, verticalSpacing);
    }

    public void ShowRange(CharacterUnit attacker, CharacterUnit defender, int minRange, int maxRange)
    {
        ShowRange(attacker, defender, minRange, maxRange, defaultDuration, fadeDuration);
    }

    public void ShowRange(CharacterUnit attacker, CharacterUnit defender, int minRange, int maxRange, float visibleDuration)
    {
        ShowRange(attacker, defender, minRange, maxRange, visibleDuration, fadeDuration);
    }

    public void ShowRange(CharacterUnit attacker, CharacterUnit defender, int minRange, int maxRange, float visibleDuration, float postHitFadeDuration)
    {
        if (attacker == null || defender == null)
            return;

        AttackRangeDisplayType displayType = GetDisplayType(attacker);
        if (!IsRangeTypeVisible(displayType))
            return;

        SpriteRenderer sortingReference = useAttackerSortingLayer
            ? attacker.GetComponentInChildren<SpriteRenderer>(true)
            : null;

        ShowRange(attacker.transform.position, defender.transform.position, minRange, maxRange, visibleDuration, postHitFadeDuration, sortingReference, displayType);
    }

    public void ShowHoverRange(CharacterUnit attacker, CharacterUnit defender, int minRange, int maxRange)
    {
        if (attacker == null || defender == null)
        {
            ClearHoverRange();
            return;
        }

        AttackRangeDisplayType displayType = GetDisplayType(attacker);
        if (!IsRangeTypeVisible(displayType))
        {
            ClearHoverRange();
            return;
        }

        SpriteRenderer sortingReference = useAttackerSortingLayer
            ? attacker.GetComponentInChildren<SpriteRenderer>(true)
            : null;

        ShowHoverRange(attacker.transform.position, defender.transform.position, minRange, maxRange, sortingReference, displayType);
    }

    public void ShowHoverRange(Vector3 attackerPos, Vector3 defenderPos, int minRange, int maxRange, SpriteRenderer sortingReference)
    {
        ShowHoverRange(attackerPos, defenderPos, minRange, maxRange, sortingReference, AttackRangeDisplayType.Enemy);
    }

    public void ShowHoverRange(
        Vector3 attackerPos,
        Vector3 defenderPos,
        int minRange,
        int maxRange,
        SpriteRenderer sortingReference,
        AttackRangeDisplayType displayType)
    {
        if (!IsRangeTypeVisible(displayType))
        {
            ClearHoverRange();
            return;
        }

        EnsureSprites();

        if (minRange <= 0 || maxRange <= 0 || maxRange < minRange)
        {
            ClearHoverRange();
            return;
        }

        ResolvePreviewRoot();
        ClearHoverRange();

        Color styleFill = GetFillColor(displayType);
        Color styleBorder = GetBorderColor(displayType);
        int forwardSign = defenderPos.x >= attackerPos.x ? 1 : -1;
        hoverGroup = new GameObject(HoverGroupName);
        hoverDisplayType = displayType;
        hoverGroup.transform.SetParent(previewRoot != null ? previewRoot : transform, false);
        hoverGroup.transform.position = Vector3.zero;
        hoverGroup.transform.rotation = Quaternion.identity;
        hoverGroup.transform.localScale = Vector3.one;

        for (int range = minRange; range <= maxRange; range++)
        {
            Vector3 cellPos = attackerPos + new Vector3(forwardSign * tileWidth * range, 0f, 0f) + worldOffset;
            SpawnCell(hoverGroup.transform, cellPos, sortingReference, displayType, styleFill, styleBorder);
        }

        if (pulseBorder)
            hoverPulseCoroutine = StartCoroutine(PulseHoverGroup(hoverGroup, styleBorder));
    }

    public void ShowRange(
        Vector3 attackerPos,
        Vector3 defenderPos,
        int minRange,
        int maxRange,
        float visibleDuration,
        float postHitFadeDuration,
        SpriteRenderer sortingReference)
    {
        ShowRange(attackerPos, defenderPos, minRange, maxRange, visibleDuration, postHitFadeDuration, sortingReference, AttackRangeDisplayType.Enemy);
    }

    public void ShowRange(
        Vector3 attackerPos,
        Vector3 defenderPos,
        int minRange,
        int maxRange,
        float visibleDuration,
        float postHitFadeDuration,
        SpriteRenderer sortingReference,
        AttackRangeDisplayType displayType)
    {
        if (!IsRangeTypeVisible(displayType))
            return;

        EnsureSprites();

        if (minRange <= 0 || maxRange <= 0 || maxRange < minRange)
            return;

        ResolvePreviewRoot();
        RemoveNullGroups();

        Color styleFill = GetFillColor(displayType);
        Color styleBorder = GetBorderColor(displayType);
        List<GameObject> targetGroups = GetGroups(displayType);
        int forwardSign = defenderPos.x >= attackerPos.x ? 1 : -1;
        GameObject group = new GameObject(TimedGroupName);
        group.transform.SetParent(previewRoot != null ? previewRoot : transform, false);
        group.transform.position = Vector3.zero;
        group.transform.rotation = Quaternion.identity;
        group.transform.localScale = Vector3.one;
        targetGroups.Add(group);

        for (int range = minRange; range <= maxRange; range++)
        {
            Vector3 cellPos = attackerPos + new Vector3(forwardSign * tileWidth * range, 0f, 0f) + worldOffset;
            SpawnCell(group.transform, cellPos, sortingReference, displayType, styleFill, styleBorder);
        }

        StartCoroutine(PulseAndFadeGroup(group, visibleDuration, postHitFadeDuration, styleBorder, targetGroups));
    }

    public void ClearAll()
    {
        StopAllCoroutines();
        hoverPulseCoroutine = null;
        hoverGroup = null;

        Transform root = previewRoot != null ? previewRoot : transform;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child != null && (child.name == TimedGroupName || child.name == HoverGroupName))
                Destroy(child.gameObject);
        }

        myRangeGroups.Clear();
        enemyRangeGroups.Clear();
    }

    public void ClearTimedRanges()
    {
        ClearRangeGroups(myRangeGroups);
        ClearRangeGroups(enemyRangeGroups);
    }

    public void ClearHoverRange()
    {
        if (hoverPulseCoroutine != null)
        {
            StopCoroutine(hoverPulseCoroutine);
            hoverPulseCoroutine = null;
        }

        if (hoverGroup != null)
            Destroy(hoverGroup);

        hoverGroup = null;
    }

    private void SpawnCell(
        Transform parent,
        Vector3 worldPos,
        SpriteRenderer sortingReference,
        AttackRangeDisplayType displayType,
        Color styleFillColor,
        Color styleBorderColor)
    {
        GameObject cell = new GameObject("AttackRangeCell");
        cell.transform.SetParent(parent, false);
        cell.transform.position = worldPos;
        cell.transform.rotation = Quaternion.identity;
        cell.transform.localScale = Vector3.one;

        int displayOrderOffset = displayType == AttackRangeDisplayType.My ? 0 : 4;
        SpriteRenderer fill = CreateRendererChild("Fill", cell.transform, generatedFillSprite, styleFillColor, displayOrderOffset, fillScale);
        SpriteRenderer border = CreateRendererChild("GlowBorder", cell.transform, generatedBorderSprite, styleBorderColor, displayOrderOffset + 1, borderScale);

        ApplySorting(fill, sortingReference, displayOrderOffset);
        ApplySorting(border, sortingReference, displayOrderOffset + 1);
        ApplyRangeMaterial(fill, false, displayType);
        ApplyRangeMaterial(border, true, displayType);
    }

    private void ApplyRangeMaterial(SpriteRenderer renderer, bool border, AttackRangeDisplayType displayType)
    {
        if (rangePresentationMaterial == null)
            rangePresentationMaterial = Resources.Load<Material>("Combat/Materials/RangeTelegraph");
        if (rangePresentationMaterial == null || !rangePresentationMaterial.shader.isSupported)
            return;

        // Both layers need a full quad. Legacy border geometry contains only its opaque rim.
        renderer.sprite = generatedFillSprite;
        renderer.sharedMaterial = rangePresentationMaterial;
        if (presentationBlock == null) presentationBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(presentationBlock);
        presentationBlock.SetFloat(RangeLayerId, border ? 1 : 0);
        presentationBlock.SetFloat(EnemyStyleId, displayType == AttackRangeDisplayType.Enemy ? 1 : 0);
        presentationBlock.SetFloat(ProgressId, -1);
        renderer.SetPropertyBlock(presentationBlock);
    }

    private SpriteRenderer CreateRendererChild(string childName, Transform parent, Sprite sprite, Color color, int sortingOffset, float scaleMultiplier)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = new Vector3(tileWidth * scaleMultiplier, tileHeight * scaleMultiplier, 1f);

        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder + sortingOffset;
        return renderer;
    }

    private void ApplySorting(SpriteRenderer renderer, SpriteRenderer sortingReference, int layerOffset)
    {
        if (renderer == null)
            return;

        if (sortingReference != null)
        {
            renderer.sortingLayerID = sortingReference.sortingLayerID;
            renderer.sortingOrder = sortingReference.sortingOrder + attackerSortingOrderOffset + layerOffset;
        }
        else
        {
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder + layerOffset;
        }
    }

    private IEnumerator PulseAndFadeGroup(
        GameObject group,
        float visibleDuration,
        float postHitFadeDuration,
        Color styleBorderColor,
        List<GameObject> ownerGroups)
    {
        float visible = Mathf.Max(0.01f, visibleDuration);
        float timer = 0f;
        SpriteRenderer[] renderers = group != null ? group.GetComponentsInChildren<SpriteRenderer>(true) : null;

        while (timer < visible && group != null)
        {
            timer += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(timer / visible);
            ApplyPulse(renderers, normalized, styleBorderColor, true);
            yield return null;
        }

        yield return FadeAndDestroy(group, postHitFadeDuration, ownerGroups);
    }

    private IEnumerator PulseHoverGroup(GameObject group, Color styleBorderColor)
    {
        SpriteRenderer[] renderers = group != null ? group.GetComponentsInChildren<SpriteRenderer>(true) : null;
        float timer = 0f;

        while (group != null)
        {
            timer += Time.unscaledDeltaTime;
            float normalized = Mathf.PingPong(timer * 1.5f, 1f);
            ApplyPulse(renderers, normalized, styleBorderColor);
            yield return null;
        }
    }

    private void ApplyPulse(SpriteRenderer[] renderers, float normalized, Color styleBorderColor, bool timed = false)
    {
        if (renderers == null)
            return;

        float pulse = Mathf.Sin(normalized * Mathf.PI);
        float alphaMultiplier = Mathf.Lerp(pulseMinAlpha, 1f, pulse);
        float scaleMultiplier = Mathf.Lerp(1f, pulseScale, pulse);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.name != "GlowBorder")
                continue;

            if (renderer.sharedMaterial == rangePresentationMaterial && rangePresentationMaterial != null)
            {
                renderer.GetPropertyBlock(presentationBlock);
                presentationBlock.SetFloat(ProgressId, timed ? normalized : -1);
                renderer.SetPropertyBlock(presentationBlock);
            }
            if (!pulseBorder) continue;

            Color color = styleBorderColor;
            color.a *= alphaMultiplier;
            renderer.color = color;

            Transform target = renderer.transform;
            target.localScale = new Vector3(tileWidth * borderScale * scaleMultiplier, tileHeight * borderScale * scaleMultiplier, 1f);
        }
    }

    private IEnumerator FadeAndDestroy(GameObject group, float duration, List<GameObject> ownerGroups)
    {
        if (group == null)
            yield break;

        SpriteRenderer[] renderers = group.GetComponentsInChildren<SpriteRenderer>(true);
        Color[] startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            startColors[i] = renderers[i] != null ? renderers[i].color : Color.clear;

        float fade = Mathf.Max(0.01f, duration);
        float timer = 0f;
        while (timer < fade && group != null)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / fade);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color color = startColors[i];
                color.a = Mathf.Lerp(startColors[i].a, 0f, t);
                renderers[i].color = color;
            }

            yield return null;
        }

        if (group != null)
        {
            if (ownerGroups != null)
                ownerGroups.Remove(group);

            Destroy(group);
        }
    }

    private void ResolvePreviewRoot()
    {
        if (previewRoot != null)
            return;

        Transform found = transform.Find("AttackRangePreviewRoot");
        if (found != null)
        {
            previewRoot = found;
            return;
        }

        GameObject root = new GameObject("AttackRangePreviewRoot");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        previewRoot = root.transform;
    }

    private void EnsureCanvasToggles()
    {
        if (!autoCreateCanvasToggle)
            return;

        UpgradeLegacyToggleReference();

        if (myRangeToggleButton == null)
        {
            GameObject existing = GameObject.Find("MyRangeToggleButton");
            if (existing != null)
                myRangeToggleButton = existing.GetComponent<Button>();
        }

        if (enemyRangeToggleButton == null)
        {
            GameObject existing = GameObject.Find("EnemyRangeToggleButton");
            if (existing != null)
                enemyRangeToggleButton = existing.GetComponent<Button>();
        }

        if (myRangeToggleButton == null)
            myRangeToggleButton = CreateCanvasToggleButton("MyRangeToggleButton", "MY RANGE", toggleAnchoredPosition);

        if (enemyRangeToggleButton == null)
            enemyRangeToggleButton = CreateCanvasToggleButton("EnemyRangeToggleButton", "ENEMY RANGE", toggleAnchoredPosition + new Vector2(0f, -(toggleSize.y + 6f)));

        if (myRangeToggleButton != null)
        {
            myRangeToggleButton.onClick.RemoveListener(ToggleMyRangeEnabled);
            myRangeToggleButton.onClick.AddListener(ToggleMyRangeEnabled);
        }

        if (enemyRangeToggleButton != null)
        {
            enemyRangeToggleButton.onClick.RemoveListener(ToggleEnemyRangeEnabled);
            enemyRangeToggleButton.onClick.AddListener(ToggleEnemyRangeEnabled);
        }

        ResolveToggleReferences();
    }

    private void UpgradeLegacyToggleReference()
    {
        if (myRangeToggleButton != null)
            return;

        if (rangeToggleButton == null)
        {
            GameObject existing = GameObject.Find("RangeVisibilityToggle");
            if (existing == null)
                existing = GameObject.Find("AttackRangeToggleButton");

            if (existing != null)
                rangeToggleButton = existing.GetComponent<Button>();
        }

        if (rangeToggleButton == null)
            return;

        myRangeToggleButton = rangeToggleButton;
        if (myRangeToggleButton.gameObject.name == "RangeVisibilityToggle" ||
            myRangeToggleButton.gameObject.name == "AttackRangeToggleButton")
        {
            myRangeToggleButton.gameObject.name = "MyRangeToggleButton";
        }
    }

    private void ResolveToggleReferences()
    {
        if (myRangeToggleButton != null)
        {
            if (myRangeToggleImage == null)
                myRangeToggleImage = myRangeToggleButton.GetComponent<Image>();

            if (myRangeToggleLabel == null)
                myRangeToggleLabel = myRangeToggleButton.GetComponentInChildren<Text>(true);
        }

        if (enemyRangeToggleButton != null)
        {
            if (enemyRangeToggleImage == null)
                enemyRangeToggleImage = enemyRangeToggleButton.GetComponent<Image>();

            if (enemyRangeToggleLabel == null)
                enemyRangeToggleLabel = enemyRangeToggleButton.GetComponentInChildren<Text>(true);
        }
    }

    private Button CreateCanvasToggleButton(string objectName, string label, Vector2 anchoredPosition)
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return null;

        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = toggleSize;
        rect.anchoredPosition = anchoredPosition;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.12f, 0.18f, 0.23f, 0.9f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonObject.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text labelText = labelObject.GetComponent<Text>();
        labelText.text = label;
        labelText.font = GetDefaultFont();
        labelText.fontSize = 13;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.raycastTarget = false;

        return buttonObject.GetComponent<Button>();
    }

    private void UpdateToggleVisual()
    {
        RangeTogglePresentation.Refresh(myRangeToggleButton, myRangeToggleImage, myRangeToggleLabel, showMyRange, true);
        RangeTogglePresentation.Refresh(enemyRangeToggleButton, enemyRangeToggleImage, enemyRangeToggleLabel, showEnemyRange, false);
    }

    private void PositionButtonBelow(RectTransform rect, RectTransform anchor, float verticalSpacing)
    {
        if (rect == null || anchor == null)
            return;

        RectTransform anchorParent = anchor.parent as RectTransform;
        if (anchorParent != null && rect.parent != anchorParent)
            rect.SetParent(anchorParent, false);

        float anchorWidth = anchor.rect.width > 0f ? anchor.rect.width : anchor.sizeDelta.x;
        float anchorHeight = anchor.rect.height > 0f ? anchor.rect.height : anchor.sizeDelta.y;
        Vector2 anchorCenter = anchor.anchoredPosition + new Vector2(
            anchorWidth * (0.5f - anchor.pivot.x),
            anchorHeight * (0.5f - anchor.pivot.y)
        );
        float anchorBottom = anchorCenter.y - anchorHeight * 0.5f;

        rect.anchorMin = anchor.anchorMin;
        rect.anchorMax = anchor.anchorMax;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = toggleSize;
        rect.anchoredPosition = new Vector2(anchorCenter.x, anchorBottom - Mathf.Max(0f, verticalSpacing));
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private void SyncLegacyVisibilityFlag()
    {
        if (!showAttackRange)
        {
            showMyRange = false;
            showEnemyRange = false;
        }

        showAttackRange = ShowAttackRange;
    }

    private AttackRangeDisplayType GetDisplayType(CharacterUnit attacker)
    {
        return attacker != null && attacker.IsMine()
            ? AttackRangeDisplayType.My
            : AttackRangeDisplayType.Enemy;
    }

    private bool IsRangeTypeVisible(AttackRangeDisplayType displayType)
    {
        return displayType == AttackRangeDisplayType.My ? showMyRange : showEnemyRange;
    }

    private Color GetFillColor(AttackRangeDisplayType displayType)
    {
        return displayType == AttackRangeDisplayType.My ? myFillColor : fillColor;
    }

    private Color GetBorderColor(AttackRangeDisplayType displayType)
    {
        return displayType == AttackRangeDisplayType.My ? myBorderColor : borderColor;
    }

    private List<GameObject> GetGroups(AttackRangeDisplayType displayType)
    {
        return displayType == AttackRangeDisplayType.My ? myRangeGroups : enemyRangeGroups;
    }

    private void ClearRangeGroups(List<GameObject> groups)
    {
        if (groups == null)
            return;

        for (int i = groups.Count - 1; i >= 0; i--)
        {
            if (groups[i] != null)
                Destroy(groups[i]);
        }

        groups.Clear();
    }

    private Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        return font;
    }

    private void EnsureSprites()
    {
        if (generatedFillSprite == null)
        {
            generatedFillTexture = CreateSolidTexture(2, 2, Color.white);
            generatedFillSprite = Sprite.Create(
                generatedFillTexture,
                new Rect(0f, 0f, generatedFillTexture.width, generatedFillTexture.height),
                new Vector2(0.5f, 0.5f),
                generatedFillTexture.width);
            generatedFillSprite.hideFlags = HideFlags.HideAndDontSave;
            generatedFillSprite.name = "RuntimeRangeFillSquare";
        }

        if (generatedBorderSprite == null)
        {
            generatedBorderTexture = CreateBorderTexture(64, 64);
            generatedBorderSprite = Sprite.Create(
                generatedBorderTexture,
                new Rect(0f, 0f, generatedBorderTexture.width, generatedBorderTexture.height),
                new Vector2(0.5f, 0.5f),
                generatedBorderTexture.width);
            generatedBorderSprite.hideFlags = HideFlags.HideAndDontSave;
            generatedBorderSprite.name = "RuntimeRangeGlowBorder";
        }
    }

    private Texture2D CreateSolidTexture(int width, int height, Color color)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, color);
        }

        texture.Apply(false, true);
        return texture;
    }

    private Texture2D CreateBorderTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
                float alpha = 0f;

                if (edge < 3)
                    alpha = 1f;
                else if (edge < 9)
                    alpha = Mathf.Lerp(0.45f, 0f, (edge - 3f) / 6f);

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        return texture;
    }

    private void RemoveNullGroups()
    {
        RemoveNullGroups(myRangeGroups);
        RemoveNullGroups(enemyRangeGroups);
    }

    private void RemoveNullGroups(List<GameObject> groups)
    {
        if (groups == null)
            return;

        for (int i = groups.Count - 1; i >= 0; i--)
        {
            if (groups[i] == null)
                groups.RemoveAt(i);
        }
    }
}
