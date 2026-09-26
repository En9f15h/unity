using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectionPrivacyController : MonoBehaviour
{
    [SerializeField] private Toggle publicSelectionToggle;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Sprite defaultToggleSprite;
    [SerializeField] private Sprite pressedToggleSprite;

    private CharacterSelectionManager manager;
    private bool suppressEvent;
    private Image toggleImage;
    private Image checkmarkImage;

    public Toggle PublicSelectionToggle => publicSelectionToggle;

    private void Awake()
    {
        CacheToggleImages();
        ConfigureImageToggle();

        if (publicSelectionToggle != null)
            publicSelectionToggle.onValueChanged.AddListener(OnToggleChanged);

        NormalizeLabel();
        ApplyToggleVisual(publicSelectionToggle != null && publicSelectionToggle.isOn);
    }

    public void Initialize(CharacterSelectionManager owner)
    {
        manager = owner;
    }

    public void SetValue(bool value, bool interactable)
    {
        suppressEvent = true;
        if (publicSelectionToggle != null)
        {
            publicSelectionToggle.isOn = value;
            publicSelectionToggle.interactable = interactable;
        }
        suppressEvent = false;

        if (statusText != null)
            statusText.text = string.Empty;

        NormalizeLabel();
        ApplyToggleVisual(value);
    }

    private void OnToggleChanged(bool value)
    {
        ApplyToggleVisual(value);

        if (suppressEvent || manager == null)
            return;

        manager.RequestPublicSelectionChange(value);
    }

    private void CacheToggleImages()
    {
        if (publicSelectionToggle == null)
            return;

        toggleImage = publicSelectionToggle.targetGraphic as Image;
        if (toggleImage == null)
            toggleImage = publicSelectionToggle.GetComponentInChildren<Image>(true);

        checkmarkImage = publicSelectionToggle.graphic as Image;
        if (checkmarkImage == null && toggleImage != null)
        {
            Transform checkmark = toggleImage.transform.Find("Checkmark");
            if (checkmark != null)
                checkmarkImage = checkmark.GetComponent<Image>();
        }

        if (defaultToggleSprite == null && toggleImage != null)
            defaultToggleSprite = toggleImage.sprite;

        if (pressedToggleSprite == null && checkmarkImage != null)
            pressedToggleSprite = checkmarkImage.sprite;
    }

    private void ConfigureImageToggle()
    {
        if (publicSelectionToggle == null)
            return;

        publicSelectionToggle.transition = Selectable.Transition.None;

        Image hitArea = publicSelectionToggle.GetComponent<Image>();
        if (hitArea != null)
        {
            hitArea.enabled = true;
            hitArea.color = new Color(1f, 1f, 1f, 0f);
            hitArea.raycastTarget = true;
        }

        if (toggleImage != null)
        {
            publicSelectionToggle.targetGraphic = toggleImage;
            toggleImage.enabled = true;
            toggleImage.color = Color.white;
            toggleImage.preserveAspect = true;
            toggleImage.raycastTarget = false;
        }

        if (checkmarkImage != null)
        {
            checkmarkImage.enabled = false;
            checkmarkImage.raycastTarget = false;
        }

        publicSelectionToggle.graphic = null;
    }

    private void ApplyToggleVisual(bool value)
    {
        if (toggleImage == null)
            CacheToggleImages();

        if (toggleImage == null)
            return;

        Sprite sprite = value ? pressedToggleSprite : defaultToggleSprite;
        if (sprite == null)
            sprite = defaultToggleSprite != null ? defaultToggleSprite : pressedToggleSprite;

        toggleImage.sprite = sprite;
        toggleImage.enabled = sprite != null;
    }

    private void NormalizeLabel()
    {
        if (publicSelectionToggle == null)
            return;

        TMP_Text[] labels = publicSelectionToggle.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null || labels[i] == statusText)
                continue;

            labels[i].text = string.Empty;
        }
    }
}
