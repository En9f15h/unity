using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

// Keeps the existing portrait/card layouts, reserving the middle column for skill details.
[DisallowMultipleComponent]
public sealed class SelectionPresentationLayout : MonoBehaviour
{
    [SerializeField] private RectTransform youPanel, enemyPanel, mapPanel;
    [SerializeField] private CharacterSkillPopup youSkills, enemySkills;
    [SerializeField] private GameObject volumePanel;
    [SerializeField] private Button volumeButton;
    [SerializeField] private TMP_Text volumeLabel;
    private Vector2 lastSize;
    private bool layingOut;
    public bool VolumeOpen => volumePanel != null && volumePanel.activeSelf;

    private void Awake() { SetVolumeOpen(false); ApplyLayout(); }
    private void OnEnable()
    {
        if (volumeButton != null) volumeButton.onClick.AddListener(ToggleVolume);
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        RefreshLabel(); ApplyLayout();
    }
    private void OnDisable()
    {
        if (volumeButton != null) volumeButton.onClick.RemoveListener(ToggleVolume);
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        SetVolumeOpen(false);
    }
    private void OnLocaleChanged(Locale locale) => RefreshLabel();
    private void RefreshLabel()
    {
        if (volumeLabel == null) return;
        string code = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en";
        volumeLabel.text = (code.StartsWith("zh") || code.StartsWith("ja") ? "音量" : "Audio") + (VolumeOpen ? "  ×" : "");
    }
    public void ToggleVolume() => SetVolumeOpen(!VolumeOpen);
    public void SetVolumeOpen(bool open)
    {
        if (volumePanel != null) volumePanel.SetActive(open);
        RefreshLabel();
    }
    private void OnRectTransformDimensionsChange()
    {
        if (Application.isPlaying && isActiveAndEnabled) ApplyLayout();
    }
    private void LateUpdate()
    {
        var rect = (RectTransform)transform;
        if (rect.rect.size != lastSize) ApplyLayout();
    }
    public void ApplyLayout()
    {
        if (layingOut || youPanel == null || enemyPanel == null || mapPanel == null) return;
        layingOut = true;
        try
        {
            var root = (RectTransform)transform;
            lastSize = root.rect.size;
            float reserved = youPanel.rect.width + enemyPanel.rect.width + 2 * 52;
            float available = Mathf.Max(300, lastSize.x - reserved);
            Vector2 position = mapPanel.anchoredPosition; position.y = 24; mapPanel.anchoredPosition = position;
            float bottom = 24 + mapPanel.rect.height + 18;
            bool sideBySide = available >= 900;
            float width = sideBySide ? Mathf.Min(520, (available - 20) * .5f) : Mathf.Min(700, available);
            float height = sideBySide ? 240 : 188;
            Place(youSkills, sideBySide ? -(width + 20) * .5f : 0, bottom, width, height);
            Place(enemySkills, sideBySide ? (width + 20) * .5f : 0, sideBySide ? bottom : bottom + height + 12, width, height);
        }
        finally { layingOut = false; }
    }
    private static void Place(CharacterSkillPopup popup, float x, float y, float width, float height)
    {
        if (popup == null) return;
        var rect = (RectTransform)popup.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, 0);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        popup.ApplyCompactLayout(width, height);
    }
}
