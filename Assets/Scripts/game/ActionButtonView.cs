using UnityEngine;
using UnityEngine.UI;

public class ActionButtonView : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text costText;
    [SerializeField] private Text rangeText;
    [SerializeField] private Image disabledOverlay;
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private Image usedOverlay;

    [Header("Tooltip")]
    [SerializeField] private Text tooltipTitleText;
    [SerializeField] private Text tooltipDescriptionText;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Bind(ActionData actionData)
    {
        ResolveReferences();

        if (actionData == null)
            return;

        Sprite icon = ResolveIcon(actionData);
        if (iconImage != null && icon != null)
            iconImage.sprite = icon;

        SetOverlaySprite(disabledOverlay, actionData.disabledOverlaySprite);
        SetOverlaySprite(cooldownOverlay, actionData.cooldownOverlaySprite);
        SetOverlaySprite(usedOverlay, actionData.usedOverlaySprite);

        if (costText != null)
            costText.text = actionData.GetSlotCost().ToString();

        if (rangeText != null)
            rangeText.text = actionData.rangeText;

        if (tooltipTitleText != null)
            tooltipTitleText.text = string.IsNullOrEmpty(actionData.tooltipTitle) ? actionData.actionName : actionData.tooltipTitle;

        if (tooltipDescriptionText != null)
            tooltipDescriptionText.text = actionData.tooltipDescription;

        SetAvailability(true, false, false);
    }

    public void SetAvailability(bool available, bool cooldown, bool used)
    {
        SetOverlay(disabledOverlay, !available && !cooldown && !used);
        SetOverlay(cooldownOverlay, cooldown);
        SetOverlay(usedOverlay, used);
    }

    private void ResolveReferences()
    {
        if (iconImage == null)
            iconImage = FindImageByName("icon", "symbol");

        if (iconImage == null)
            iconImage = GetComponent<Image>();

        if (costText == null)
            costText = FindTextByName("cost", "costtext", "costbadge");

        if (rangeText == null)
            rangeText = FindTextByName("range", "rangetext", "rangebadge");

        if (disabledOverlay == null)
            disabledOverlay = FindImageByName("disabledoverlay", "disabled", "unavailable");

        if (cooldownOverlay == null)
            cooldownOverlay = FindImageByName("cooldownoverlay", "cooldown");

        if (usedOverlay == null)
            usedOverlay = FindImageByName("usedoverlay", "used");

        if (tooltipTitleText == null)
            tooltipTitleText = FindTextByName("tooltiptitle", "title");

        if (tooltipDescriptionText == null)
            tooltipDescriptionText = FindTextByName("tooltipdescription", "description", "tooltipbody");
    }

    private Sprite ResolveIcon(ActionData actionData)
    {
        if (actionData == null)
            return null;

        if (actionData.iconSprite != null)
            return actionData.iconSprite;

        if (actionData.sourcePrefab == null)
            return null;

        Image image = actionData.sourcePrefab.GetComponent<Image>();
        if (image == null || image.sprite == null)
            image = actionData.sourcePrefab.GetComponentInChildren<Image>(true);

        return image != null ? image.sprite : null;
    }

    private void SetOverlay(Image overlay, bool active)
    {
        if (overlay != null)
            overlay.gameObject.SetActive(active);
    }

    private void SetOverlaySprite(Image overlay, Sprite sprite)
    {
        if (overlay != null && sprite != null)
            overlay.sprite = sprite;
    }

    private Image FindImageByName(params string[] tokens)
    {
        Image[] images = GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null)
                continue;

            string normalized = Normalize(images[i].name);
            for (int j = 0; j < tokens.Length; j++)
            {
                if (normalized.Contains(tokens[j]))
                    return images[i];
            }
        }

        return null;
    }

    private Text FindTextByName(params string[] tokens)
    {
        Text[] texts = GetComponentsInChildren<Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null)
                continue;

            string normalized = Normalize(texts[i].name);
            for (int j = 0; j < tokens.Length; j++)
            {
                if (normalized.Contains(tokens[j]))
                    return texts[i];
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
