using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectionUILayoutController : MonoBehaviour
{
    [Header("Major Regions")]
    [SerializeField] private RectTransform topBar;
    [SerializeField] private RectTransform youPanel;
    [SerializeField] private RectTransform enemyPanel;
    [SerializeField] private RectTransform characterSelectorPanel;
    [SerializeField] private RectTransform characterCardPanel;
    [SerializeField] private RectTransform centerPanel;
    [SerializeField] private RectTransform centerVsPanel;
    [SerializeField] private RectTransform mapSelectorPanel;
    [SerializeField] private RectTransform mapSelectionPanel;
    [SerializeField] private RectTransform youSkillPopup;
    [SerializeField] private RectTransform enemySkillPopup;

    [Header("Layout Groups")]
    [SerializeField] private GridLayoutGroup characterGrid;
    [SerializeField] private HorizontalLayoutGroup mapLayout;

    [Header("Tuning")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private float sideMargin = 28f;
    [SerializeField] private float centerGap = 28f;
    [SerializeField] private float bottomMargin = 24f;
    [SerializeField] private float topBarHeight = 88f;
    [SerializeField] private bool applyLayoutAtRuntime;

    private RectTransform rootRect;

    private void Awake()
    {
        rootRect = transform as RectTransform;
        ResolveReferences();
        if (applyLayoutAtRuntime)
            ApplyLayout();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && applyLayoutAtRuntime)
            ApplyLayout();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !applyLayoutAtRuntime)
            return;

        ApplyLayout();
    }

    public void ApplyLayout()
    {
        ResolveReferences();

        if (rootRect == null)
            rootRect = transform as RectTransform;

        Vector2 size = GetCanvasSize();
        if (size.x <= 0f || size.y <= 0f)
            return;

        float topHeight = Mathf.Clamp(topBarHeight, 76f, Mathf.Min(104f, size.y * 0.12f));
        float availableTop = size.y * 0.5f - topHeight - 14f;
        float availableBottom = -size.y * 0.5f + bottomMargin;
        float availableHeight = Mathf.Max(620f, availableTop - availableBottom);
        float sideWidth = Mathf.Clamp(size.x * 0.215f, 340f, 430f);
        float sideHeight = Mathf.Min(availableHeight, Mathf.Clamp(size.y - topHeight - bottomMargin - 28f, 700f, 860f));
        float sideCenterY = (availableTop + availableBottom) * 0.5f;

        SetTopStretch(topBar, topHeight);
        ApplyTopBarInterior(topBar);

        SetSidePanel(youPanel, true, sideMargin, sideWidth, sideHeight, sideCenterY);
        SetSidePanel(enemyPanel, false, sideMargin, sideWidth, sideHeight, sideCenterY);
        ApplyPlayerPanelInterior(youPanel, true);
        ApplyPlayerPanelInterior(enemyPanel, false);

        float centerLeft = sideMargin + sideWidth + centerGap;
        float centerRight = size.x - sideMargin - sideWidth - centerGap;
        float centerWidth = Mathf.Max(420f, centerRight - centerLeft);
        float centerX = (centerLeft + centerRight - size.x) * 0.5f;

        RectTransform activeSelectorPanel = characterSelectorPanel != null ? characterSelectorPanel : characterCardPanel;
        float selectorWidth = Mathf.Min(centerWidth, centerWidth < 620f ? 520f : 610f);
        float selectorHeight = centerWidth < 620f ? 300f : 330f;
        float selectorY = Mathf.Min(availableTop - selectorHeight * 0.5f, 238f);
        SetCenter(activeSelectorPanel, centerX, selectorY, selectorWidth, selectorHeight);

        if (characterGrid != null)
        {
            float cellWidth = centerWidth < 620f ? 220f : 250f;
            float cellHeight = centerWidth < 620f ? 280f : 310f;
            characterGrid.cellSize = new Vector2(cellWidth, cellHeight);
            characterGrid.spacing = new Vector2(18f, 0f);
        }

        RectTransform activeCenterPanel = centerPanel != null ? centerPanel : centerVsPanel;
        SetCenter(activeCenterPanel, centerX, 92f, 180f, 100f);

        float mapWidth = Mathf.Min(centerWidth, 620f);
        float mapHeight = centerWidth < 620f ? 140f : 150f;
        RectTransform activeMapPanel = mapSelectorPanel != null ? mapSelectorPanel : mapSelectionPanel;
        SetBottomCenter(activeMapPanel, centerX, bottomMargin, mapWidth, mapHeight);
        ApplyMapSelectorInterior(activeMapPanel);

        if (mapLayout != null)
        {
            mapLayout.spacing = centerWidth < 620f ? 10f : 18f;
            mapLayout.childControlWidth = true;
            mapLayout.childControlHeight = true;
            mapLayout.childForceExpandWidth = false;
            mapLayout.childForceExpandHeight = false;

            RectTransform mapCardsRect = mapLayout.transform as RectTransform;
            if (mapCardsRect != null)
            {
                float mapCardsWidth = Mathf.Max(430f, mapWidth - 40f);
                float mapCardsHeight = Mathf.Max(142f, mapHeight - 74f);
                SetBottomCenter(mapCardsRect, 0f, 14f, mapCardsWidth, mapCardsHeight);
            }
        }

        float popupGap = centerWidth < 620f ? 16f : 24f;
        float popupWidth = Mathf.Min(500f, Mathf.Max(420f, centerWidth * 0.48f));
        float popupHeight = centerWidth < 620f ? 176f : 188f;
        float popupX = (popupWidth + popupGap) * 0.5f;
        float popupY = bottomMargin + mapHeight + 36f;
        SetBottomCenter(youSkillPopup, centerX - popupX, popupY, popupWidth, popupHeight);
        SetBottomCenter(enemySkillPopup, centerX + popupX, popupY, popupWidth, popupHeight);
        ApplySkillPopupInterior(youSkillPopup);
        ApplySkillPopupInterior(enemySkillPopup);

    }

    private void ResolveReferences()
    {
        if (topBar == null)
            topBar = FindChildRect("TopBar");
        if (youPanel == null)
            youPanel = FindChildRect("YouPanel");
        if (enemyPanel == null)
            enemyPanel = FindChildRect("EnemyPanel");
        if (characterSelectorPanel == null)
            characterSelectorPanel = FindChildRect("CharacterSelector");
        if (characterSelectorPanel == null)
            characterSelectorPanel = FindChildRect("CharacterSelectorPanel");
        if (characterCardPanel == null)
            characterCardPanel = FindChildRect("CharacterCardPanel");
        if (centerPanel == null)
            centerPanel = FindChildRect("CenterPanel");
        if (centerVsPanel == null)
            centerVsPanel = FindChildRect("CenterVSPanel");
        if (mapSelectorPanel == null)
            mapSelectorPanel = FindChildRect("MapSelector");
        if (mapSelectorPanel == null)
            mapSelectorPanel = FindChildRect("MapSelectorPanel");
        if (mapSelectionPanel == null)
            mapSelectionPanel = FindChildRect("MapSelectionPanel");
        if (youSkillPopup == null)
            youSkillPopup = FindChildRect("SkillPopup");
        if (enemySkillPopup == null)
            enemySkillPopup = FindChildRect("EnemySkillPopup");
        if (characterGrid == null && characterCardPanel != null)
            characterGrid = characterCardPanel.GetComponent<GridLayoutGroup>();
        if (mapLayout == null && mapSelectionPanel != null)
            mapLayout = mapSelectionPanel.GetComponentInChildren<HorizontalLayoutGroup>(true);
    }

    private Vector2 GetCanvasSize()
    {
        if (rootRect != null && rootRect.rect.width > 1f && rootRect.rect.height > 1f)
            return rootRect.rect.size;

        return referenceResolution;
    }

    private RectTransform FindChildRect(string childName)
    {
        Transform child = transform.Find(childName);
        return child as RectTransform;
    }

    private static void SetTopStretch(RectTransform rect, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, height);
    }

    private static void SetSidePanel(RectTransform rect, bool left, float margin, float width, float height, float centerY)
    {
        if (rect == null)
            return;

        float anchorX = left ? 0f : 1f;
        rect.anchorMin = new Vector2(anchorX, 0.5f);
        rect.anchorMax = new Vector2(anchorX, 0.5f);
        rect.pivot = new Vector2(anchorX, 0.5f);
        rect.anchoredPosition = new Vector2(left ? margin : -margin, centerY);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetCenter(RectTransform rect, float x, float y, float width, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetBottomCenter(RectTransform rect, float x, float y, float width, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetFullStretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void ApplyTopBarInterior(RectTransform rect)
    {
        if (rect == null)
            return;

        SetChildRect(rect, "BackButton", new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(46f, 32f));
        SetChildRect(rect, "StatusText", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 34f));
        SetChildText(rect, "StatusText", 24f, TextAlignmentOptions.Center);

        RectTransform toggle = SetChildRect(rect, "PublicSelectionToggle", new Vector2(1f, 0.5f), new Vector2(-92f, 0f), new Vector2(164f, 34f));
        if (toggle == null)
            return;

        SetChildRect(toggle, "Background", new Vector2(1f, 0.5f), new Vector2(-26f, 0f), new Vector2(46f, 26f));
        SetChildRect(toggle, "Checkmark", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 22f));
        SetChildRect(toggle, "Label", new Vector2(0f, 0.5f), new Vector2(58f, 0f), new Vector2(90f, 22f));
        SetChildText(toggle, "Label", 12f, TextAlignmentOptions.Center);
    }

    private static void ApplyPlayerPanelInterior(RectTransform panel, bool localPanel)
    {
        if (panel == null)
            return;

        SetChildText(panel, localPanel ? "YOUText" : "ENEMYText", 22f, TextAlignmentOptions.Center);
        SetChildRect(panel, localPanel ? "YOUText" : "ENEMYText", new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(230f, 30f));
        SetChildText(panel, "ReadyStateText", 14f, TextAlignmentOptions.Center);
        SetChildRect(panel, "ReadyStateText", new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(160f, 22f));

        SetChildRect(panel, "CharacterPreviewRoot", new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(210f, 205f));
        SetChildRect(panel, "PortraitFallback", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(138f, 150f));

        SetChildRect(panel, "CharacterInfo", new Vector2(0.5f, 1f), new Vector2(0f, -398f), new Vector2(318f, 106f));

        SetChildRect(panel, "HiddenSelectionRoot", new Vector2(0.5f, 1f), new Vector2(0f, -398f), new Vector2(300f, 72f));
        SetChildText(panel, "HiddenSelectionText", 18f, TextAlignmentOptions.Center);
        SetChildRect(panel, "HiddenSelectionText", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 42f));

        SetChildRect(panel, "SkinHeaderText", new Vector2(0.5f, 0f), new Vector2(0f, 304f), new Vector2(90f, 24f));
        SetChildText(panel, "SkinHeaderText", 14f, TextAlignmentOptions.Center);
        SetChildRect(panel, "PreviousSkinButton", new Vector2(0.5f, 0f), new Vector2(-116f, 304f), new Vector2(36f, 30f));
        SetChildRect(panel, "NextSkinButton", new Vector2(0.5f, 0f), new Vector2(116f, 304f), new Vector2(36f, 30f));

        SetChildRect(panel, "RadarBackgroundImage", new Vector2(0.5f, 0f), new Vector2(0f, 154f), new Vector2(214f, 178f));
        SetChildRect(panel, "RadarChartRoot", new Vector2(0.5f, 0f), new Vector2(0f, 154f), new Vector2(214f, 178f));

        SetChildRect(panel, "PreviousCharacterButton", new Vector2(0.5f, 1f), new Vector2(-144f, -398f), new Vector2(34f, 30f));
        SetChildRect(panel, "NextCharacterButton", new Vector2(0.5f, 1f), new Vector2(144f, -398f), new Vector2(34f, 30f));
        SetChildRect(panel, "ConfirmButton", new Vector2(0.5f, 0f), new Vector2(-72f, 34f), new Vector2(132f, 38f));
        SetChildRect(panel, "CancelReadyButton", new Vector2(0.5f, 0f), new Vector2(76f, 34f), new Vector2(132f, 38f));
        SetChildText(panel, "Text", 12f, TextAlignmentOptions.Center);
    }

    private static void ApplyMapSelectorInterior(RectTransform rect)
    {
        if (rect == null)
            return;

        SetChildRect(rect, "PreviousMapButton", new Vector2(0f, 0.5f), new Vector2(42f, 4f), new Vector2(38f, 34f));
        SetChildRect(rect, "NextMapButton", new Vector2(1f, 0.5f), new Vector2(-42f, 4f), new Vector2(38f, 34f));
        SetChildRect(rect, "MapPreviewImage", new Vector2(0.5f, 0.5f), new Vector2(0f, 32f), new Vector2(176f, 86f));
        SetChildRect(rect, "CurrentMapNameText", new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(360f, 26f));
        SetChildText(rect, "CurrentMapNameText", 14f, TextAlignmentOptions.Center);

        RectTransform lockRect = FindChildRecursive(rect, "HostOnlyLockImage");
        if (lockRect != null)
            SetFullStretch(lockRect);
    }

    private static void ApplySkillPopupInterior(RectTransform rect)
    {
        if (rect == null)
            return;

        SetChildRect(rect, "ButtonRoot", new Vector2(0f, 0.5f), new Vector2(64f, 0f), new Vector2(104f, 152f));
        SetChildRect(rect, "SkillIcon", new Vector2(0f, 1f), new Vector2(164f, -42f), new Vector2(44f, 44f));
        SetChildRect(rect, "SkillNameText", new Vector2(0f, 1f), new Vector2(308f, -30f), new Vector2(276f, 30f));
        SetChildRect(rect, "DescriptionText", new Vector2(0f, 1f), new Vector2(308f, -82f), new Vector2(276f, 54f));
        SetChildRect(rect, "DamageText", new Vector2(0f, 0f), new Vector2(184f, 54f), new Vector2(92f, 20f));
        SetChildRect(rect, "RangeText", new Vector2(0f, 0f), new Vector2(306f, 54f), new Vector2(128f, 20f));
        SetChildRect(rect, "SlotCostText", new Vector2(0f, 0f), new Vector2(184f, 30f), new Vector2(92f, 20f));
        SetChildRect(rect, "EnergyCostText", new Vector2(0f, 0f), new Vector2(306f, 30f), new Vector2(128f, 20f));

        SetChildText(rect, "SkillNameText", 16f, TextAlignmentOptions.Left);
        SetChildText(rect, "DescriptionText", 10f, TextAlignmentOptions.Left);
        SetChildText(rect, "DamageText", 10f, TextAlignmentOptions.Left);
        SetChildText(rect, "RangeText", 10f, TextAlignmentOptions.Left);
        SetChildText(rect, "SlotCostText", 10f, TextAlignmentOptions.Left);
        SetChildText(rect, "EnergyCostText", 10f, TextAlignmentOptions.Left);
    }

    private static RectTransform SetChildRect(RectTransform root, string childName, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform child = FindChildRecursive(root, childName);
        if (child == null)
            return null;

        child.anchorMin = anchor;
        child.anchorMax = anchor;
        child.pivot = new Vector2(0.5f, 0.5f);
        child.anchoredPosition = position;
        child.sizeDelta = size;
        child.localScale = Vector3.one;
        return child;
    }

    private static void SetChildText(RectTransform root, string childName, float fontSize, TextAlignmentOptions alignment)
    {
        RectTransform child = FindChildRecursive(root, childName);
        if (child == null)
            return;

        TMP_Text text = child.GetComponent<TMP_Text>();
        if (text == null)
            return;

        text.fontSize = fontSize;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(6f, fontSize - 4f);
        text.fontSizeMax = fontSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    private static RectTransform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root as RectTransform;

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
