using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class CharacterSkillPopup : MonoBehaviour
{
    private const string LocalizationTable = "StringTable";

    [SerializeField] private Transform buttonRoot;
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Image skillIcon;
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text rangeText;
    [SerializeField] private TMP_Text slotCostText;
    [SerializeField] private TMP_Text energyCostText;
    [SerializeField] private TMP_FontAsset localizedChineseFontAsset;
    [SerializeField] private TMP_FontAsset localizedEnglishFontAsset;
    [SerializeField] private TMP_FontAsset localizedJapaneseFontAsset;
    [SerializeField] private bool applyRuntimeLayoutOverrides;

    private readonly List<SkillDisplayButton> pooledButtons = new List<SkillDisplayButton>();
    private bool accessible = true;
    private SkillDisplayData currentSkill;

    private void Awake()
    {
        ResolveReferences();
        if (applyRuntimeLayoutOverrides)
            NormalizeLayout();
        HidePopup();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        ApplyLocalizedFonts();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    public void SetAccessible(bool value)
    {
        accessible = value;
        if (!accessible)
            HidePopup();
    }

    public void BindSkills(SkillDisplayData[] skills)
    {
        ResolveReferences();
        if (applyRuntimeLayoutOverrides) 
           NormalizeLayout();

        int count = skills != null ? skills.Length : 0;
        EnsurePool(count);

        for (int i = 0; i < pooledButtons.Count; i++)
        {
            bool active = i < count && skills[i] != null;
            pooledButtons[i].gameObject.SetActive(active);
            NormalizePooledButton(pooledButtons[i].gameObject);
            if (active)
                pooledButtons[i].Initialize(skills[i], this);
        }

        RectTransform buttonRect = buttonRoot as RectTransform;
        if (buttonRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonRect);

        if (count > 0 && skills[0] != null && accessible)
            ShowSkill(skills[0]);
        else
            HidePopup();
    }

    public void ShowSkill(SkillDisplayData skill)
    {
        if (!accessible || skill == null)
            return;

        currentSkill = skill;

        if (popupRoot != null)
            popupRoot.SetActive(true);

        if (skillIcon != null)
            skillIcon.sprite = skill.icon;

        if (skillNameText != null)
            skillNameText.text = skill.skillName.Replace("knight_", "");

        if (descriptionText != null)
            SetLocalizedText(descriptionText, GetSkillInfoKey(skill), skill.description);

        if (damageText != null)
            damageText.text = GetDamageLabel(skill);

        if (rangeText != null)
            rangeText.text = "RANGE " + skill.range;

        if (slotCostText != null)
            slotCostText.text = "SLOT " + skill.actionSlotCost;

        if (energyCostText != null)
            energyCostText.text = "ENERGY " + skill.energyCost;

        ApplyLocalizedFonts();
    }

    public void HidePopup()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);
    }
  
    public void ApplyCompactLayout(float width, float height)
    {
        // Three icon columns fit both classes while reserving a separate readable detail area.
        
        if (buttonRoot != null)
        {
            var vertical = buttonRoot.GetComponent<VerticalLayoutGroup>();
            if (vertical != null) vertical.enabled = false;
            var grid = buttonRoot.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = buttonRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(44,44); grid.spacing = new Vector2(6,6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperLeft;
            SetRect(buttonRoot as RectTransform, new Vector2(0,1), new Vector2(width*.175f,-height*.5f), new Vector2(144,144));
        }
        
        /*
        float left = width*.36f+12, detailWidth = width - left - 24;
        SetRect(skillIcon != null ? skillIcon.rectTransform : null, new Vector2(0,1), new Vector2(width-46,-42), new Vector2(44,44));
        SetRect(skillNameText != null ? skillNameText.rectTransform : null, new Vector2(0,1), new Vector2(left+(detailWidth-60)*.5f,-38), new Vector2(detailWidth-60,36));
        SetRect(descriptionText != null ? descriptionText.rectTransform : null, new Vector2(0,1), new Vector2(left+detailWidth*.5f,-height*.5f), new Vector2(detailWidth,height-106));
        float column = detailWidth*.5f;
        SetRect(damageText != null ? damageText.rectTransform : null, Vector2.zero, new Vector2(left+column*.5f,44), new Vector2(column,18));
        SetRect(rangeText != null ? rangeText.rectTransform : null, Vector2.zero, new Vector2(left+column*1.5f,44), new Vector2(column,18));
        SetRect(slotCostText != null ? slotCostText.rectTransform : null, Vector2.zero, new Vector2(left+column*.5f,24), new Vector2(column,18));
        SetRect(energyCostText != null ? energyCostText.rectTransform : null, Vector2.zero, new Vector2(left+column*1.5f,24), new Vector2(column,18));
        ConfigureText(skillNameText,16,20,TextAlignmentOptions.Left);
        ConfigureText(descriptionText,13,16,TextAlignmentOptions.TopLeft);
        foreach (var text in new[] {damageText,rangeText,slotCostText,energyCostText}) ConfigureText(text,11,13,TextAlignmentOptions.Left);
        */
    }
    
    private string GetDamageLabel(SkillDisplayData skill)
    {
        if (skill != null && string.Equals(skill.skillName, "Bolt", System.StringComparison.OrdinalIgnoreCase))
            return "DMG 1-3";

        return "DMG " + (skill != null ? skill.damage : 0);
    }

    private void EnsurePool(int count)
    {
        if (buttonPrefab == null || buttonRoot == null)
            return;

        while (pooledButtons.Count < count)
        {
            GameObject buttonObject = Instantiate(buttonPrefab, buttonRoot);
            SkillDisplayButton button = buttonObject.GetComponent<SkillDisplayButton>();
            if (button == null)
            {
                Debug.LogError("CharacterSkillPopup: skill button prefab is missing SkillDisplayButton.", buttonObject);
                Destroy(buttonObject);
                break;
            }

            NormalizePooledButton(buttonObject);
            pooledButtons.Add(button);
        }
    }

    private void ResolveReferences()
    {
        if (buttonRoot == null)
            buttonRoot = transform;

        if (popupRoot == null)
            popupRoot = gameObject;
    }

    private void NormalizeLayout()
    {
        RectTransform rootRect = popupRoot != null ? popupRoot.transform as RectTransform : transform as RectTransform;
        RectTransform buttonRect = buttonRoot as RectTransform;
        if (buttonRect != null)
            SetRect(buttonRect, new Vector2(0f, 0.5f), new Vector2(64f, 0f), new Vector2(104f, 152f));

        VerticalLayoutGroup vertical = buttonRoot != null ? buttonRoot.GetComponent<VerticalLayoutGroup>() : null;
        if (vertical != null)
            vertical.enabled = false;

        GridLayoutGroup grid = buttonRoot != null ? buttonRoot.GetComponent<GridLayoutGroup>() : null;
        if (grid == null && buttonRoot != null)
            grid = buttonRoot.gameObject.AddComponent<GridLayoutGroup>();

        if (grid != null)
        {
            grid.cellSize = new Vector2(44f, 44f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperLeft;
        }

        float width = rootRect != null && rootRect.rect.width > 1f ? rootRect.rect.width : 450f;
        float detailCenterX = Mathf.Lerp(276f, 304f, Mathf.InverseLerp(420f, 500f, width));
        float detailWidth = Mathf.Clamp(width - 186f, 210f, 290f);

        //SetRect(skillIcon != null ? skillIcon.transform as RectTransform : null, new Vector2(0f, 1f), new Vector2(164f, -42f), new Vector2(44f, 44f));
        //SetRect(skillNameText != null ? skillNameText.transform as RectTransform : null, new Vector2(0f, 1f), new Vector2(detailCenterX, -30f), new Vector2(detailWidth, 30f));
        //SetRect(descriptionText != null ? descriptionText.transform as RectTransform : null, new Vector2(0f, 1f), new Vector2(detailCenterX, -82f), new Vector2(detailWidth, 54f));
        //SetRect(damageText != null ? damageText.transform as RectTransform : null, new Vector2(0f, 0f), new Vector2(184f, 54f), new Vector2(92f, 20f));
        //SetRect(rangeText != null ? rangeText.transform as RectTransform : null, new Vector2(0f, 0f), new Vector2(306f, 54f), new Vector2(128f, 20f));
        //SetRect(slotCostText != null ? slotCostText.transform as RectTransform : null, new Vector2(0f, 0f), new Vector2(184f, 30f), new Vector2(92f, 20f));
        //SetRect(energyCostText != null ? energyCostText.transform as RectTransform : null, new Vector2(0f, 0f), new Vector2(306f, 30f), new Vector2(128f, 20f));
       //ConfigureText(skillNameText, 16f, 18f, TextAlignmentOptions.Left);
       //ConfigureText(descriptionText, 9f, 11f, TextAlignmentOptions.Left);
       //ConfigureText(damageText, 9f, 11f, TextAlignmentOptions.Left);
       //ConfigureText(rangeText, 9f, 11f, TextAlignmentOptions.Left);
       //ConfigureText(slotCostText, 9f, 11f, TextAlignmentOptions.Left);
       //ConfigureText(energyCostText, 9f, 11f, TextAlignmentOptions.Left);

        if (buttonRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonRect);
    }

    private void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void ConfigureText(TMP_Text text, float minSize, float maxSize, TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    private void NormalizePooledButton(GameObject buttonObject)
    {
        if (buttonObject == null)
            return;

        RectTransform rect = buttonObject.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(44f, 44f);
        }

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = buttonObject.AddComponent<LayoutElement>();

        layoutElement.minWidth = 44f;
        layoutElement.minHeight = 44f;
        layoutElement.preferredWidth = 44f;
        layoutElement.preferredHeight = 44f;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;
    }

    private string GetSkillInfoKey(SkillDisplayData skill)
    {
        if (skill == null || string.IsNullOrWhiteSpace(skill.skillName))
            return string.Empty;

        switch (skill.skillName.Trim())
        {
            case "knight_lightAttck":
                return "LightAttackInfo";
            case "knight_heavyAttck":
                return "HeavyAttackInfo";
            case "knight_lowAttack":
                return "LowAttackInfo";
            case "knight_parry":
                return "ParryInfo";
            case "knight_defense":
                return "DefenseInfo";
            case "Ultimate":
                return "UltimateInfo";
            default:
                return skill.skillName.Trim().Replace(" ", string.Empty) + "Info";
        }
    }

    private string GetLocalizedText(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key))
            return fallback;

        string localized = LocalizationSettings.StringDatabase.GetLocalizedString(LocalizationTable, key);
        return string.IsNullOrEmpty(localized) ? fallback : localized;
    }

    private void SetLocalizedText(TMP_Text text, string key, string fallback)
    {
        if (text == null)
            return;

        ApplyLocalizedFont(text);
        text.text = GetLocalizedText(key, fallback);
    }

    private void OnSelectedLocaleChanged(Locale locale)
    {
        if (currentSkill != null && accessible && popupRoot != null && popupRoot.activeSelf)
            ShowSkill(currentSkill);
        else
            ApplyLocalizedFonts();
    }

    private void ApplyLocalizedFonts()
    {
        ApplyLocalizedFont(skillNameText);
        ApplyLocalizedFont(descriptionText);
        ApplyLocalizedFont(damageText);
        ApplyLocalizedFont(rangeText);
        ApplyLocalizedFont(slotCostText);
        ApplyLocalizedFont(energyCostText);
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
            return localizedChineseFontAsset;

        if (code.StartsWith("ja", System.StringComparison.OrdinalIgnoreCase))
            return localizedJapaneseFontAsset != null ? localizedJapaneseFontAsset : localizedEnglishFontAsset;

        return localizedEnglishFontAsset;
    }
}
