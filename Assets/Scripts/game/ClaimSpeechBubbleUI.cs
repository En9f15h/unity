using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ClaimSpeechBubbleUI : MonoBehaviour
{
    [SerializeField] private RectTransform slotRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Text titleText;
    [SerializeField] private float fadeDuration = 0.12f;

    [Header("Optional Runtime Formatting")]
    [SerializeField] private bool applyRuntimeVisualDefaults;
    [SerializeField] private bool applyRuntimeSlotRootFormat;
    [SerializeField] private Vector2 runtimeSlotRootAnchorMin = new Vector2(0.04f, 0.05f);
    [SerializeField] private Vector2 runtimeSlotRootAnchorMax = new Vector2(0.96f, 0.68f);
    [SerializeField] private RectOffset runtimeSlotPadding;
    [SerializeField] private float runtimeSlotSpacing = 8f;
    [SerializeField] private TextAnchor runtimeSlotAlignment = TextAnchor.MiddleCenter;

    private Coroutine fadeCoroutine;

    public RectTransform RectTransform { get; private set; }
    public RectTransform SlotRoot => slotRoot;
    private bool warnedMissingStaticParts;

    private void Awake()
    {
        ResolveReferences();
    }

    public void ResolveReferences()
    {
        if (RectTransform == null)
            RectTransform = GetComponent<RectTransform>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (applyRuntimeVisualDefaults && backgroundImage != null)
        {
            backgroundImage.color = new Color(0.03f, 0.08f, 0.11f, 0.88f);
            backgroundImage.raycastTarget = false;
        }

        if (titleText == null)
            titleText = FindChildText("ClaimTitle");

        if (applyRuntimeVisualDefaults && titleText != null)
        {
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontSize = 14;
            titleText.color = new Color(0.85f, 0.96f, 1f, 1f);
        }

        if (slotRoot == null)
        {
            Transform found = transform.Find("ClaimSlotRoot");
            if (found != null)
                slotRoot = found as RectTransform;
        }

        if (canvasGroup == null || slotRoot == null)
            WarnMissingStaticParts();

        if (slotRoot == null)
            return;

        HorizontalLayoutGroup layout = slotRoot.GetComponent<HorizontalLayoutGroup>();
        if (applyRuntimeSlotRootFormat)
            ApplySlotRootFormat(layout);
    }

    public void ConfigureNear(RectTransform anchor, bool above, int slotCount)
    {
        ResolveReferences();

        float width = Mathf.Max(240f, slotCount * 58f + 34f);
        float height = 86f;

        RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        if (anchor == null)
            return;

        RectTransform parent = anchor.parent as RectTransform;
        if (parent != null && RectTransform.parent != parent)
            RectTransform.SetParent(parent, false);

        RectTransform.anchorMin = anchor.anchorMin;
        RectTransform.anchorMax = anchor.anchorMax;
        RectTransform.pivot = new Vector2(0.5f, above ? 0f : 1f);

        float yOffset = above
            ? anchor.rect.height * 0.5f + 18f
            : -(anchor.rect.height * 0.5f + 18f);

        RectTransform.anchoredPosition = anchor.anchoredPosition + new Vector2(0f, yOffset);
        RectTransform.localScale = Vector3.one;
        RectTransform.localRotation = Quaternion.identity;
    }

    public void ConfigureAlignedBelow(RectTransform actionRow, ActionSlot[] actionSlots, float verticalSpacing)
    {
        ResolveReferences();

        Vector2 slotSize = GetReferenceSlotSize(actionSlots);
        HorizontalLayoutGroup sourceLayout = actionRow != null ? actionRow.GetComponent<HorizontalLayoutGroup>() : null;
        RectOffset padding = CopyPadding(sourceLayout != null ? sourceLayout.padding : null);
        float spacing = sourceLayout != null ? sourceLayout.spacing : 0f;
        TextAnchor alignment = sourceLayout != null ? sourceLayout.childAlignment : TextAnchor.MiddleCenter;
        float rowWidth = GetReferenceRowWidth(actionRow, actionSlots, slotSize, spacing, padding);

        if (actionRow != null)
        {
            RectTransform parent = actionRow.parent as RectTransform;
            if (parent != null && RectTransform.parent != parent)
                RectTransform.SetParent(parent, false);

            RectTransform.anchorMin = actionRow.anchorMin;
            RectTransform.anchorMax = actionRow.anchorMax;
            RectTransform.pivot = actionRow.pivot;
            RectTransform.anchoredPosition = actionRow.anchoredPosition + new Vector2(0f, -(slotSize.y + verticalSpacing));
        }

        RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rowWidth);
        RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, slotSize.y);
        RectTransform.localScale = Vector3.one;
        RectTransform.localRotation = Quaternion.identity;

        if (slotRoot == null)
            return;

        if (titleText != null)
            titleText.gameObject.SetActive(false);

        slotRoot.anchorMin = Vector2.zero;
        slotRoot.anchorMax = Vector2.one;
        slotRoot.offsetMin = Vector2.zero;
        slotRoot.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layout = slotRoot.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = slotRoot.gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.childAlignment = alignment;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.spacing = spacing;
        layout.padding = padding;
    }

    public void CopySlotRootLayoutFrom(ClaimSpeechBubbleUI source)
    {
        ResolveReferences();
        if (source == null || slotRoot == null || source.SlotRoot == null)
            return;

        RectTransform sourceRoot = source.SlotRoot;
        slotRoot.anchorMin = sourceRoot.anchorMin;
        slotRoot.anchorMax = sourceRoot.anchorMax;
        slotRoot.pivot = sourceRoot.pivot;
        slotRoot.offsetMin = sourceRoot.offsetMin;
        slotRoot.offsetMax = sourceRoot.offsetMax;
        slotRoot.localScale = Vector3.one;
        slotRoot.localRotation = Quaternion.identity;

        HorizontalLayoutGroup sourceLayout = sourceRoot.GetComponent<HorizontalLayoutGroup>();
        if (sourceLayout == null)
            return;

        HorizontalLayoutGroup layout = slotRoot.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = slotRoot.gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.childAlignment = sourceLayout.childAlignment;
        layout.childForceExpandWidth = sourceLayout.childForceExpandWidth;
        layout.childForceExpandHeight = sourceLayout.childForceExpandHeight;
        layout.childControlWidth = sourceLayout.childControlWidth;
        layout.childControlHeight = sourceLayout.childControlHeight;
        layout.childScaleWidth = sourceLayout.childScaleWidth;
        layout.childScaleHeight = sourceLayout.childScaleHeight;
        layout.reverseArrangement = sourceLayout.reverseArrangement;
        layout.spacing = sourceLayout.spacing;
        layout.padding = CopyPadding(sourceLayout.padding);
    }

    public void SetTitle(string value)
    {
        ResolveReferences();
        if (titleText != null)
            titleText.text = value;
    }

    public void SetVisible(bool visible, bool animate)
    {
        ResolveReferences();
        gameObject.SetActive(true);

        if (canvasGroup == null)
        {
            gameObject.SetActive(visible);
            return;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        float target = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;

        if (!animate || fadeDuration <= 0f)
        {
            canvasGroup.alpha = target;
            RectTransform.localScale = Vector3.one;
            if (!visible)
                gameObject.SetActive(false);
            return;
        }

        fadeCoroutine = StartCoroutine(FadeTo(target, visible));
    }

    private IEnumerator FadeTo(float target, bool visible)
    {
        float start = canvasGroup.alpha;
        Vector3 startScale = RectTransform.localScale;
        Vector3 hiddenScale = Vector3.one * 0.95f;
        Vector3 overshootScale = Vector3.one * 1.03f;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(start, target, t);

            if (visible)
            {
                RectTransform.localScale = t < 0.65f
                    ? Vector3.Lerp(startScale, overshootScale, t / 0.65f)
                    : Vector3.Lerp(overshootScale, Vector3.one, (t - 0.65f) / 0.35f);
            }
            else
            {
                RectTransform.localScale = Vector3.Lerp(startScale, hiddenScale, t);
            }

            yield return null;
        }

        canvasGroup.alpha = target;
        RectTransform.localScale = Vector3.one;
        fadeCoroutine = null;

        if (!visible)
            gameObject.SetActive(false);
    }

    private Text FindChildText(string childName)
    {
        Text[] texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name == childName)
                return texts[i];
        }

        return null;
    }

    private Vector2 GetReferenceSlotSize(ActionSlot[] actionSlots)
    {
        if (actionSlots != null)
        {
            for (int i = 0; i < actionSlots.Length; i++)
            {
                RectTransform rect = actionSlots[i] != null ? actionSlots[i].GetComponent<RectTransform>() : null;
                Vector2 size = GetRectSize(rect);
                if (size.x > 0f && size.y > 0f)
                    return size;
            }
        }

        return new Vector2(100f, 100f);
    }

    private float GetReferenceRowWidth(RectTransform actionRow, ActionSlot[] actionSlots, Vector2 slotSize, float spacing, RectOffset padding)
    {
        if (actionRow != null && actionRow.rect.width > 0f)
            return actionRow.rect.width;

        int count = actionSlots != null ? actionSlots.Length : 0;
        if (count <= 0)
            return slotSize.x;

        float paddingWidth = padding != null ? padding.horizontal : 0f;
        return paddingWidth + (slotSize.x * count) + (spacing * Mathf.Max(0, count - 1));
    }

    private Vector2 GetRectSize(RectTransform rect)
    {
        if (rect == null)
            return Vector2.zero;

        Vector2 size = rect.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = rect.sizeDelta;

        return size;
    }

    private RectOffset CopyPadding(RectOffset source)
    {
        if (source == null)
            return new RectOffset();

        return new RectOffset(source.left, source.right, source.top, source.bottom);
    }

    private void ApplySlotRootFormat(HorizontalLayoutGroup layout)
    {
        slotRoot.anchorMin = runtimeSlotRootAnchorMin;
        slotRoot.anchorMax = runtimeSlotRootAnchorMax;
        slotRoot.offsetMin = Vector2.zero;
        slotRoot.offsetMax = Vector2.zero;

        if (layout == null)
            layout = slotRoot.gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.childAlignment = runtimeSlotAlignment;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.spacing = runtimeSlotSpacing;
        layout.padding = GetRuntimeSlotPadding();
    }

    private RectOffset GetRuntimeSlotPadding()
    {
        if (runtimeSlotPadding == null)
            runtimeSlotPadding = new RectOffset(8, 8, 2, 2);

        return CopyPadding(runtimeSlotPadding);
    }

    private void WarnMissingStaticParts()
    {
        if (warnedMissingStaticParts)
            return;

        warnedMissingStaticParts = true;
        Debug.LogWarning($"[Claim] {name} is missing static UI parts. Expected CanvasGroup and ClaimSlotRoot.");
    }
}
