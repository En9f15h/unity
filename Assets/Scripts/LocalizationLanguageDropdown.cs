using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class LocalizationLanguageDropdown : MonoBehaviour
{
    private const string SelectedLocalePrefKey = "SelectedLocaleCode";

    [SerializeField] private Dropdown languageDropdown;

    private readonly List<Locale> locales = new List<Locale>();
    private bool suppressDropdownEvent;

    private IEnumerator Start()
    {
        ResolveDropdown();
        if (languageDropdown == null)
        {
            Debug.LogWarning("LocalizationLanguageDropdown requires a Dropdown assigned in the scene hierarchy.");
            yield break;
        }

        yield return LocalizationSettings.InitializationOperation;

        ApplySavedLocale();
        PopulateDropdown();
        SyncItemTextSizeWithCaption();
        UpdateDropdownValue();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        if (languageDropdown != null)
            languageDropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
    }

    private void ResolveDropdown()
    {
        if (languageDropdown == null)
            languageDropdown = GetComponent<Dropdown>();

        if (languageDropdown == null)
        {
            GameObject dropdownObject = GameObject.Find("LanguageDropdown");
            if (dropdownObject != null)
                languageDropdown = dropdownObject.GetComponent<Dropdown>();
        }

        if (languageDropdown == null)
            return;

        languageDropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        languageDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    private void PopulateDropdown()
    {
        locales.Clear();

        IReadOnlyList<Locale> availableLocales = LocalizationSettings.AvailableLocales.Locales;
        for (int i = 0; i < availableLocales.Count; i++)
        {
            if (availableLocales[i] != null)
                locales.Add(availableLocales[i]);
        }

        languageDropdown.ClearOptions();

        List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
        for (int i = 0; i < locales.Count; i++)
            options.Add(new Dropdown.OptionData(GetLocaleDisplayName(locales[i])));

        languageDropdown.AddOptions(options);
        languageDropdown.interactable = locales.Count > 1;
        SyncItemTextSizeWithCaption();
    }

    private void ApplySavedLocale()
    {
        string savedCode = PlayerPrefs.GetString(SelectedLocalePrefKey, string.Empty);
        if (string.IsNullOrEmpty(savedCode))
            return;

        IReadOnlyList<Locale> availableLocales = LocalizationSettings.AvailableLocales.Locales;
        for (int i = 0; i < availableLocales.Count; i++)
        {
            Locale locale = availableLocales[i];
            if (locale != null && locale.Identifier.Code == savedCode)
            {
                LocalizationSettings.SelectedLocale = locale;
                return;
            }
        }
    }

    private void OnDropdownValueChanged(int index)
    {
        if (suppressDropdownEvent || index < 0 || index >= locales.Count)
            return;

        Locale locale = locales[index];
        if (locale == null)
            return;

        LocalizationSettings.SelectedLocale = locale;
        PlayerPrefs.SetString(SelectedLocalePrefKey, locale.Identifier.Code);
        PlayerPrefs.Save();
    }

    private void OnSelectedLocaleChanged(Locale locale)
    {
        UpdateDropdownValue();
    }

    private void UpdateDropdownValue()
    {
        if (languageDropdown == null)
            return;

        Locale selectedLocale = LocalizationSettings.SelectedLocale;
        int selectedIndex = -1;
        for (int i = 0; i < locales.Count; i++)
        {
            if (selectedLocale != null && locales[i] == selectedLocale)
            {
                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex < 0 && locales.Count > 0)
            selectedIndex = 0;

        if (selectedIndex < 0)
            return;

        suppressDropdownEvent = true;
        languageDropdown.SetValueWithoutNotify(selectedIndex);
        languageDropdown.RefreshShownValue();
        SyncItemTextSizeWithCaption();
        suppressDropdownEvent = false;
    }

    private void SyncItemTextSizeWithCaption()
    {
        if (languageDropdown == null || languageDropdown.captionText == null || languageDropdown.itemText == null)
            return;

        Text captionText = languageDropdown.captionText;
        Text itemText = languageDropdown.itemText;
        itemText.fontSize = captionText.fontSize;
        itemText.resizeTextForBestFit = captionText.resizeTextForBestFit;
        itemText.resizeTextMinSize = captionText.resizeTextMinSize;
        itemText.resizeTextMaxSize = captionText.resizeTextMaxSize;
    }

    private string GetLocaleDisplayName(Locale locale)
    {
        if (locale == null)
            return string.Empty;

        string code = locale.Identifier.Code;
        if (code == "zh-TW")
            return "繁體中文";

        if (code == "en")
            return "English";

        if (code == "ja-JP" || code == "ja")
            return "日本語";

        return string.IsNullOrEmpty(locale.LocaleName) ? code : locale.LocaleName;
    }
}
