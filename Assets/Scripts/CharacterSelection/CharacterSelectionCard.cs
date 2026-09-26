using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public enum CharacterSelectionCardVisualState
{
    Normal,
    Hover,
    YouSelected,
    EnemySelected,
    BothSelected,
    Locked,
    ReadyLocked,
    HiddenEnemySelection
}

public class CharacterSelectionCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const string LocalizationTable = "StringTable";
    private const string ChineseFontResourcePath = "Fonts & Materials/MSJHL SDF";
    private const string EnglishFontResourcePath = "Fonts & Materials/LiberationSans SDF";
    private const string JapaneseFontResourcePath = "Fonts & Materials/YuGothL SDF";

    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private GameObject youMarker;
    [SerializeField] private GameObject enemyMarker;
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button button;
    [SerializeField] private Animator animator;
    [SerializeField] private Color normalColor = new Color(0.12f, 0.13f, 0.18f, 0.95f);
    [SerializeField] private Color hoverColor = new Color(0.17f, 0.22f, 0.30f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.18f, 0.36f, 0.48f, 1f);
    [SerializeField] private Color bothSelectedColor = new Color(0.38f, 0.28f, 0.48f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.07f, 0.07f, 0.09f, 0.9f);

    [Header("Localization Fonts")]
    [SerializeField] private TMP_FontAsset localizedChineseFontAsset;
    [SerializeField] private TMP_FontAsset localizedEnglishFontAsset;
    [SerializeField] private TMP_FontAsset localizedJapaneseFontAsset;

    private CharacterSelectionManager manager;
    private CharacterSelectionDefinition definition;
    private CharacterSelectionCardVisualState state;
    private bool interactable = true;
    private bool buttonWired;

    public CharacterSelectionDefinition Definition => definition;

    private void Awake()
    {
        ResolveReferences();
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

    public void Initialize(CharacterSelectionDefinition newDefinition, CharacterSelectionManager owner)
    {
        definition = newDefinition;
        manager = owner;
        ApplyDefinition();
        SetVisualState(CharacterSelectionCardVisualState.Normal);
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        if (button != null)
            button.interactable = value;

        if (!interactable && state == CharacterSelectionCardVisualState.Hover)
            SetVisualState(CharacterSelectionCardVisualState.Normal);
    }

    public void SetMarkers(bool youSelected, bool enemySelected, bool hiddenEnemySelection, bool readyLocked)
    {
        if (youMarker != null)
            youMarker.SetActive(youSelected);

        if (enemyMarker != null)
            enemyMarker.SetActive(enemySelected && !hiddenEnemySelection);

        if (hiddenEnemySelection)
        {
            SetVisualState(youSelected ? CharacterSelectionCardVisualState.YouSelected : CharacterSelectionCardVisualState.HiddenEnemySelection);
            return;
        }

        if (readyLocked)
        {
            SetVisualState(CharacterSelectionCardVisualState.ReadyLocked);
            return;
        }

        if (youSelected && enemySelected)
            SetVisualState(CharacterSelectionCardVisualState.BothSelected);
        else if (youSelected)
            SetVisualState(CharacterSelectionCardVisualState.YouSelected);
        else if (enemySelected)
            SetVisualState(CharacterSelectionCardVisualState.EnemySelected);
        else
            SetVisualState(CharacterSelectionCardVisualState.Normal);
    }

    public void SetVisualState(CharacterSelectionCardVisualState newState)
    {
        state = newState;

        if (backgroundImage != null)
        {
            switch (state)
            {
                case CharacterSelectionCardVisualState.Hover:
                    backgroundImage.color = hoverColor;
                    break;
                case CharacterSelectionCardVisualState.YouSelected:
                case CharacterSelectionCardVisualState.EnemySelected:
                    backgroundImage.color = selectedColor;
                    break;
                case CharacterSelectionCardVisualState.BothSelected:
                    backgroundImage.color = bothSelectedColor;
                    break;
                case CharacterSelectionCardVisualState.Locked:
                case CharacterSelectionCardVisualState.ReadyLocked:
                    backgroundImage.color = lockedColor;
                    break;
                default:
                    backgroundImage.color = normalColor;
                    break;
            }
        }

        if (lockOverlay != null)
            lockOverlay.SetActive(state == CharacterSelectionCardVisualState.Locked || state == CharacterSelectionCardVisualState.ReadyLocked);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!interactable || definition == null || manager == null)
            return;

        manager.PreviewCharacter(definition);
        SetVisualState(CharacterSelectionCardVisualState.Hover);
        PlayTrigger("CardHoverIn");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (manager != null)
            manager.RestoreCommittedPreview();

        PlayTrigger("CardHoverOut");
    }

    private void OnDestroy()
    {
        if (button != null && buttonWired)
            button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        if (!interactable || definition == null || manager == null)
            return;

        manager.CommitCharacterSelection(definition);
        PlayTrigger("CardSelectedPulse");
    }

    private void ApplyDefinition()
    {
        if (definition == null)
            return;

        if (portraitImage != null)
            portraitImage.sprite = definition.cardSprite != null ? definition.cardSprite : definition.portraitSprite;

        RefreshLocalizedClassName();

        if (roleText != null)
        {
            roleText.text = string.Empty;
            roleText.gameObject.SetActive(false);
        }

        if (difficultyText != null)
        {
            difficultyText.text = string.Empty;
            difficultyText.gameObject.SetActive(false);
        }
    }

    private void ResolveReferences()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
            button = gameObject.AddComponent<Button>();

        button.targetGraphic = backgroundImage;
        button.transition = Selectable.Transition.None;
        button.interactable = interactable;

        if (!buttonWired)
        {
            button.onClick.AddListener(HandleClick);
            buttonWired = true;
        }

        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void PlayTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName) || !HasTrigger(triggerName))
            return;

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
    }

    private bool HasTrigger(string triggerName)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].name == triggerName)
                return true;
        }

        return false;
    }

    private void RefreshLocalizedClassName()
    {
        if (characterNameText == null || definition == null)
            return;

        ApplyLocalizedFont(characterNameText);
        characterNameText.text = GetLocalizedClassName(definition);
    }

    private string GetLocalizedClassName(CharacterSelectionDefinition characterDefinition)
    {
        if (characterDefinition == null)
            return string.Empty;

        string fallback = characterDefinition.GetDisplayNameFallback();
        string key = characterDefinition.GetClassLocalizationKey();
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
