using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class CharacterSelectionPlayerPanel : MonoBehaviour
{
    private const string LocalizationTable = "StringTable";
    private const string ChineseFontResourcePath = "Fonts & Materials/MSJHL SDF";
    private const string EnglishFontResourcePath = "Fonts & Materials/LiberationSans SDF";
    private const string JapaneseFontResourcePath = "Fonts & Materials/YuGothL SDF";

    [Header("Player Identity")]
    [SerializeField] private TMP_Text perspectiveTitleText;
    [SerializeField] private Image crownImage;
    [FormerlySerializedAs("playerNameText")]
    [SerializeField] private TMP_Text legacyNameText;
    [SerializeField] private TMP_Text readyStateText;

    [Header("Character Info")]
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text skinText;
    [SerializeField] private GameObject characterInfoRoot;
    [SerializeField] private GameObject hiddenSelectionRoot;
    [SerializeField] private TMP_Text hiddenSelectionText;

    [Header("Preview")]
    [SerializeField] private GameObject characterPreviewRoot;
    [SerializeField] private GameObject radarChartRoot;
    [SerializeField] private CharacterPreviewController previewController;
    [SerializeField] private CharacterRadarChartController radarChartController;

    [Header("Controls")]
    [SerializeField] private Button previousCharacterButton;
    [SerializeField] private Button nextCharacterButton;
    [SerializeField] private Button previousSkinButton;
    [SerializeField] private Button nextSkinButton;
    [SerializeField] private Button skillButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmButtonText;
    [SerializeField] private Button cancelReadyButton;
    [SerializeField] private GameObject readyEffectRoot;

    [Header("Configurable Images")]
    [SerializeField] private Image panelBackgroundImage;
    [SerializeField] private Image characterPreviewBackgroundImage;
    [SerializeField] private Image characterInfoBackgroundImage;
    [SerializeField] private bool allowRuntimeImageOverrides;

    [Header("Localization Fonts")]
    [SerializeField] private TMP_FontAsset localizedChineseFontAsset;
    [SerializeField] private TMP_FontAsset localizedEnglishFontAsset;
    [SerializeField] private TMP_FontAsset localizedJapaneseFontAsset;

    [Header("Waiting Overlay")]
    [SerializeField] private GameObject waitingOverlayRoot;
    [SerializeField] private Image waitingOverlayImage;
    [SerializeField] private TMP_Text waitingOverlayText;

    private Player boundPlayer;
    private CharacterSelectionDefinition currentDefinition;
    private bool isYouPanel;
    private bool capturedImageDefaults;
    private Sprite defaultPanelBackgroundSprite;
    private Sprite defaultCharacterPreviewBackgroundSprite;
    private Sprite defaultCharacterInfoBackgroundSprite;

    public Button PreviousCharacterButton => previousCharacterButton;
    public Button NextCharacterButton => nextCharacterButton;
    public Button PreviousSkinButton => previousSkinButton;
    public Button NextSkinButton => nextSkinButton;
    public Button SkillButton => skillButton;
    public Button ConfirmButton => confirmButton;
    public Button CancelReadyButton => cancelReadyButton;

    private void Awake()
    {
        ResolveReferences();
        CaptureImageDefaults();
        HideNonSkillDescriptionText();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        RefreshLocalizedClassName();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public void BindAsYou(Player player)
    {
        isYouPanel = true;
        boundPlayer = player;
        ClearLegacyNameText();
        SetPerspectiveTitle("YOU");
        if (previewController != null)
            previewController.SetFacingDirection(1);
    }

    public void BindAsEnemy(Player player)
    {
        isYouPanel = false;
        boundPlayer = player;
        ClearLegacyNameText();
        SetPerspectiveTitle("ENEMY");
        if (previewController != null)
            previewController.SetFacingDirection(-1);
    }

    public void SetCrownVisible(bool visible)
    {
        if (crownImage != null)
            crownImage.gameObject.SetActive(visible);
    }

    public void ClearCharacter()
    {
        boundPlayer = null;
        SetReadyState(false);
        ShowWaitingForEnemy();
    }

    public void ShowWaitingForEnemy()
    {
        currentDefinition = null;
        HideClassNameText();
        SetCharacterInfoVisible(false);
        SetHiddenVisible(true, isYouPanel ? "SELECT" : "WAITING FOR ENEMY");

        if (previewController != null)
            previewController.ShowHiddenSilhouette();

        if (radarChartController != null)
            radarChartController.Clear();

        ApplySelectionImages(null);
    }

    public void SetCharacter(CharacterSelectionDefinition definition, int skinIndex)
    {
        if (definition == null)
        {
            ShowWaitingForEnemy();
            return;
        }

        currentDefinition = definition;
        SetWaitingOverlayVisible(false);
        SetHiddenVisible(false, string.Empty);
        SetCharacterInfoVisible(true);
        ApplySelectionImages(definition);

        RefreshLocalizedClassName();
        SetTextInactive(skinText);
        SetTextInactive(legacyNameText);

        if (previewController != null)
            previewController.ShowCharacter(definition, skinIndex);

        if (radarChartController != null)
            radarChartController.ApplyDefinition(definition, true, false);
    }

    public void ShowHiddenSelection()
    {
        currentDefinition = null;
        HideClassNameText();
        SetWaitingOverlayVisible(false);
        SetCharacterInfoVisible(false);
        SetHiddenVisible(true, "ENEMY");

        if (previewController != null)
            previewController.ShowHiddenSilhouette();

        if (radarChartController != null)
            radarChartController.Clear();

        ApplySelectionImages(null);
    }

    public void SetReadyState(bool ready)
    {
        if (confirmButton != null)
            UIShaderFeedback.Ensure(confirmButton.image).SetState(ready ? 1f : 0f, ready);
        if (readyStateText != null)
            readyStateText.text = ready ? "READY" : "SELECT";

        if (readyEffectRoot != null)
            readyEffectRoot.SetActive(ready);

        if (previewController != null && ready)
            previewController.PlayReady();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        SetInteractable(enabled);
    }

    public void SetInteractable(bool enabled)
    {
        if (previousCharacterButton != null)
            previousCharacterButton.interactable = enabled;
        if (nextCharacterButton != null)
            nextCharacterButton.interactable = enabled;
        if (previousSkinButton != null)
            previousSkinButton.interactable = enabled;
        if (nextSkinButton != null)
            nextSkinButton.interactable = enabled;
        if (skillButton != null)
            skillButton.interactable = true;
        if (confirmButton != null)
            confirmButton.interactable = enabled;
    }

    public void SetReadyControls(bool ready)
    {
        if (confirmButtonText == null && confirmButton != null)
            confirmButtonText = confirmButton.GetComponentInChildren<TMP_Text>(true);

        if (confirmButtonText != null)
            confirmButtonText.text = ready ? "READY" : "SELECT";

        if (cancelReadyButton != null)
            cancelReadyButton.gameObject.SetActive(ready);
    }

    public void SetWaitingOverlayVisible(bool visible)
    {
        EnsureWaitingOverlay();

        if (waitingOverlayRoot != null)
            waitingOverlayRoot.SetActive(visible);

        if (waitingOverlayText != null)
            waitingOverlayText.text = "Loading";
    }

    public void PlayReveal()
    {
        if (previewController != null)
            previewController.PlayReveal();

        if (radarChartController != null)
            radarChartController.PlayReveal();
    }

    private void ResolveReferences()
    {
        if (characterInfoRoot == null)
            characterInfoRoot = FindChildGameObject("CharacterInfo");

        if (characterPreviewRoot == null)
            characterPreviewRoot = FindChildGameObject("CharacterPreviewRoot");

        if (radarChartRoot == null)
            radarChartRoot = FindChildGameObject("RadarChartRoot");

        if (previewController == null)
        {
            if (characterPreviewRoot != null)
                previewController = characterPreviewRoot.GetComponentInChildren<CharacterPreviewController>(true);
            if (previewController == null)
                previewController = GetComponentInChildren<CharacterPreviewController>(true);
        }

        if (characterPreviewRoot == null && previewController != null)
            characterPreviewRoot = previewController.gameObject;

        if (radarChartController == null)
        {
            if (radarChartRoot != null)
                radarChartController = radarChartRoot.GetComponentInChildren<CharacterRadarChartController>(true);
            if (radarChartController == null)
                radarChartController = GetComponentInChildren<CharacterRadarChartController>(true);
        }

        if (radarChartRoot == null && radarChartController != null)
            radarChartRoot = radarChartController.gameObject;

        if (confirmButtonText == null && confirmButton != null)
            confirmButtonText = confirmButton.GetComponentInChildren<TMP_Text>(true);

        if (legacyNameText == null)
        {
            Transform found = transform.Find("PlayerNameText");
            if (found != null)
                legacyNameText = found.GetComponent<TMP_Text>();
        }

        if (panelBackgroundImage == null)
            panelBackgroundImage = GetComponent<Image>();

        if (characterPreviewBackgroundImage == null && characterPreviewRoot != null)
            characterPreviewBackgroundImage = characterPreviewRoot.GetComponent<Image>();

        if (characterInfoBackgroundImage == null && characterInfoRoot != null)
            characterInfoBackgroundImage = characterInfoRoot.GetComponent<Image>();
    }

    private void EnsureWaitingOverlay()
    {
        if (waitingOverlayRoot != null)
            return;

        RectTransform parentRect = GetComponent<RectTransform>();
        if (parentRect == null)
            return;

        GameObject overlay = new GameObject("WaitingOverlay", typeof(RectTransform), typeof(Image));
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.SetParent(transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();

        waitingOverlayImage = overlay.GetComponent<Image>();
        waitingOverlayImage.color = new Color(0.25f, 0.25f, 0.25f, 0.62f);
        waitingOverlayImage.raycastTarget = false;

        GameObject textObject = new GameObject("LoadingText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(overlayRect, false);
        textRect.anchorMin = new Vector2(0.08f, 0.42f);
        textRect.anchorMax = new Vector2(0.92f, 0.58f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        waitingOverlayText = textObject.GetComponent<TMP_Text>();
        waitingOverlayText.alignment = TextAlignmentOptions.Center;
        waitingOverlayText.fontSize = 28f;
        waitingOverlayText.color = Color.white;
        waitingOverlayText.text = "Loading";

        waitingOverlayRoot = overlay;
    }

    private GameObject FindChildGameObject(string childName)
    {
        Transform found = FindChildRecursive(transform, childName);
        return found != null ? found.gameObject : null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void SetPerspectiveTitle(string value)
    {
        if (perspectiveTitleText != null)
            perspectiveTitleText.text = value;
    }

    private void ClearLegacyNameText()
    {
        if (legacyNameText == null)
            return;

        legacyNameText.text = string.Empty;
        legacyNameText.gameObject.SetActive(false);
    }

    private void HideNonSkillDescriptionText()
    {
        SetTextInactive(legacyNameText);
        SetTextInactive(roleText);
        SetTextInactive(skinText);
    }

    private void CaptureImageDefaults()
    {
        if (capturedImageDefaults)
            return;

        defaultPanelBackgroundSprite = panelBackgroundImage != null ? panelBackgroundImage.sprite : null;
        defaultCharacterPreviewBackgroundSprite = characterPreviewBackgroundImage != null ? characterPreviewBackgroundImage.sprite : null;
        defaultCharacterInfoBackgroundSprite = characterInfoBackgroundImage != null ? characterInfoBackgroundImage.sprite : null;
        capturedImageDefaults = true;
    }

    private void ApplySelectionImages(CharacterSelectionDefinition definition)
    {
        ResolveReferences();
        CaptureImageDefaults();

        if (!allowRuntimeImageOverrides)
            return;

        ApplyImage(panelBackgroundImage, definition != null && definition.selectionPanelBackgroundSprite != null
            ? definition.selectionPanelBackgroundSprite
            : defaultPanelBackgroundSprite);

        ApplyImage(characterPreviewBackgroundImage, definition != null && definition.characterPreviewBackgroundSprite != null
            ? definition.characterPreviewBackgroundSprite
            : defaultCharacterPreviewBackgroundSprite);

        ApplyImage(characterInfoBackgroundImage, definition != null && definition.characterInfoBackgroundSprite != null
            ? definition.characterInfoBackgroundSprite
            : defaultCharacterInfoBackgroundSprite);
    }

    private void ApplyImage(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
    }

    private void SetTextInactive(TMP_Text text)
    {
        if (text == null)
            return;

        text.text = string.Empty;
        text.gameObject.SetActive(false);
    }

    private void SetCharacterInfoVisible(bool visible)
    {
        if (characterInfoRoot != null)
            characterInfoRoot.SetActive(visible);
    }

    private void SetHiddenVisible(bool visible, string label)
    {
        if (hiddenSelectionRoot != null)
            hiddenSelectionRoot.SetActive(visible);

        if (hiddenSelectionText != null)
            hiddenSelectionText.text = label;
    }

    private void RefreshLocalizedClassName()
    {
        if (roleText == null || currentDefinition == null)
            return;

        ApplyLocalizedFont(roleText);
        roleText.text = GetLocalizedClassName(currentDefinition);
        roleText.gameObject.SetActive(true);
    }

    private void HideClassNameText()
    {
        SetTextInactive(roleText);
    }

    private string GetLocalizedClassName(CharacterSelectionDefinition definition)
    {
        if (definition == null)
            return string.Empty;

        string fallback = definition.GetDisplayNameFallback();
        string key = definition.GetClassLocalizationKey();
        if (string.IsNullOrEmpty(key))
            return fallback;

        string localized = LocalizationSettings.StringDatabase.GetLocalizedString(LocalizationTable, key);
        return string.IsNullOrEmpty(localized) ? fallback : localized;
    }

    private void OnSelectedLocaleChanged(Locale locale)
    {
        RefreshLocalizedClassName();
    }

    private void ApplyLocalizedFont(TMP_Text text)
    {
        TMP_FontAsset font = GetFontForSelectedLocale();
        if (text == null || font == null)
            return;

        text.font = font;
        text.fontSharedMaterial = font.material;
    }

    private TMP_FontAsset GetFontForSelectedLocale()
    {
        Locale locale = LocalizationSettings.SelectedLocale;
        string code = locale != null ? locale.Identifier.Code : string.Empty;
        if (code.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase))
            return LoadFont(ref localizedChineseFontAsset, ChineseFontResourcePath);

        if (code.StartsWith("ja", System.StringComparison.OrdinalIgnoreCase))
            return LoadFont(ref localizedJapaneseFontAsset, JapaneseFontResourcePath) ??
                   LoadFont(ref localizedEnglishFontAsset, EnglishFontResourcePath);

        return LoadFont(ref localizedEnglishFontAsset, EnglishFontResourcePath);
    }

    private static TMP_FontAsset LoadFont(ref TMP_FontAsset font, string resourcePath)
    {
        if (font == null && !string.IsNullOrEmpty(resourcePath))
            font = Resources.Load<TMP_FontAsset>(resourcePath);

        return font;
    }
}
