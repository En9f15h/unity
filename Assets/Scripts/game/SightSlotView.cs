using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SightSlotView : MonoBehaviour
{
    private static readonly string[] DefaultWatchedSlotSpriteResourcePaths =
    {
        "Prefab/Oracle/UI/Sprites/Oracle_SightFrame",
        "Character/Oracle/Sight"
    };

    [Header("Watched Slot Root")]
    [SerializeField] private Sprite sightWatchedSlotSprite;
    [SerializeField] private Image slotRootImage;

    [Header("Content")]
    [SerializeField] private Image actionIcon;

    [Header("Reveal Layer")]
    [SerializeField] private GameObject sightRevealLayer;
    [SerializeField] private Image revealedActionIcon;
    [SerializeField] private Text emptyIndicator;
    [SerializeField] private Image lockedContinuationIndicator;

    [Header("Watched Slot Polish")]
    [SerializeField] private float watchedSlotPulseDuration = 0.18f;
    [SerializeField] private float watchedSlotPulseScale = 1.08f;

    private bool referencesResolved;
    private Image watchedSpriteTarget;
    private bool normalIconVisualCaptured;
    private bool watchedVisualApplied;
    private Sprite normalActionIconSprite;
    private Color normalActionIconColor = Color.white;
    private Image.Type normalActionIconImageType = Image.Type.Simple;
    private bool normalActionIconPreserveAspect;
    private bool normalActionIconEnabled = true;
    private Coroutine watchedPulseCoroutine;

    private void Awake()
    {
        ResolveReferences();
        SetWatched(false);
        ClearReveal();
    }

    public void ResolveReferences()
    {
        if (referencesResolved)
            return;

        if (slotRootImage == null)
            slotRootImage = GetComponent<Image>();

        if (actionIcon == null)
            actionIcon = FindImageByName("ActionIcon", "Icon", "ContentIcon");

        if (sightRevealLayer == null)
            sightRevealLayer = FindChildByName("SightRevealLayer");

        if (revealedActionIcon == null)
            revealedActionIcon = FindImageByName("RevealedActionIcon");

        if (emptyIndicator == null)
            emptyIndicator = FindTextByName("EmptyIndicator");

        if (lockedContinuationIndicator == null)
            lockedContinuationIndicator = FindImageByName("LockedContinuationIndicator");

        referencesResolved = true;
    }

    public Image GetOrCreateActionIconImage()
    {
        ResolveReferences();

        if (actionIcon == null)
        {
            actionIcon = CreateStretchImage("ActionIcon", transform, Color.white);
            actionIcon.preserveAspect = true;
        }

        return actionIcon;
    }

    public void SetWatchedSpriteTarget(Image target)
    {
        if (watchedVisualApplied)
            SetWatched(false);

        watchedSpriteTarget = target != null ? target : slotRootImage;

        normalIconVisualCaptured = false;
    }

    public void SetWatchedSlotSprite(Sprite sprite)
    {
        if (sprite == null)
            return;

        sightWatchedSlotSprite = sprite;

        if (!watchedVisualApplied)
            return;

        Image target = watchedSpriteTarget != null ? watchedSpriteTarget : slotRootImage;
        if (target != null)
            target.sprite = sightWatchedSlotSprite;
    }

    public void SetWatched(bool watched)
    {
        ResolveReferences();

        Image target = watchedSpriteTarget != null ? watchedSpriteTarget : slotRootImage;
        if (target == null)
            return;

        if (watched)
        {
            if (!watchedVisualApplied)
                CaptureNormalActionIconVisual(target);

            if (sightWatchedSlotSprite == null)
                sightWatchedSlotSprite = LoadDefaultWatchedSlotSprite();

            if (sightWatchedSlotSprite != null)
            {
                target.sprite = sightWatchedSlotSprite;
                target.type = Image.Type.Simple;
                target.preserveAspect = true;
                target.color = Color.white;
                target.enabled = true;
                watchedVisualApplied = true;
            }

            return;
        }

        if (!watchedVisualApplied)
            return;

        if (!normalIconVisualCaptured)
            CaptureNormalActionIconVisual(target);

        target.sprite = normalActionIconSprite;
        target.color = normalActionIconColor;
        target.type = normalActionIconImageType;
        target.preserveAspect = normalActionIconPreserveAspect;
        target.enabled = normalActionIconEnabled;

        watchedVisualApplied = false;
        normalIconVisualCaptured = false;
    }

    public void ClearReveal()
    {
        ResolveReferences();

        if (sightRevealLayer != null)
            sightRevealLayer.SetActive(false);

        if (revealedActionIcon != null)
        {
            revealedActionIcon.enabled = false;
            revealedActionIcon.sprite = null;
        }

        if (emptyIndicator != null)
            emptyIndicator.gameObject.SetActive(false);

        if (lockedContinuationIndicator != null)
        {
            lockedContinuationIndicator.enabled = false;
            lockedContinuationIndicator.sprite = null;
        }
    }

    public void ShowSnapshot(SlotSnapshot snapshot, Sprite actionSprite, Sprite emptySprite)
    {
        ResolveReferences();
        EnsureRevealLayer();
        SetWatched(true);

        if (sightRevealLayer != null)
            sightRevealLayer.SetActive(true);

        if (snapshot.isLockedContinuation)
        {
            ShowLockedContinuation(actionSprite != null ? actionSprite : emptySprite);
            PlayWatchedSlotPulse();
            return;
        }

        if (snapshot.isEmpty || snapshot.actionId == (int)ActionType.None)
        {
            ShowEmpty(emptySprite);
            PlayWatchedSlotPulse();
            return;
        }

        ShowAction(actionSprite != null ? actionSprite : emptySprite);
        PlayWatchedSlotPulse();
    }

    private void CaptureNormalActionIconVisual(Image target)
    {
        if (normalIconVisualCaptured || target == null)
            return;

        normalActionIconSprite = target.sprite;
        normalActionIconColor = target.color;
        normalActionIconImageType = target.type;
        normalActionIconPreserveAspect = target.preserveAspect;
        normalActionIconEnabled = target.enabled;
        normalIconVisualCaptured = true;
    }

    private void ShowAction(Sprite sprite)
    {
        if (revealedActionIcon != null)
        {
            revealedActionIcon.sprite = sprite;
            revealedActionIcon.color = Color.white;
            revealedActionIcon.enabled = sprite != null;
        }

        if (emptyIndicator != null)
            emptyIndicator.gameObject.SetActive(false);

        if (lockedContinuationIndicator != null)
            lockedContinuationIndicator.enabled = false;
    }

    private void ShowEmpty(Sprite sprite)
    {
        if (revealedActionIcon != null)
        {
            revealedActionIcon.sprite = sprite;
            revealedActionIcon.color = new Color(1f, 1f, 1f, 0.65f);
            revealedActionIcon.enabled = sprite != null;
        }

        if (emptyIndicator != null)
        {
            emptyIndicator.text = "-";
            emptyIndicator.gameObject.SetActive(sprite == null);
        }

        if (lockedContinuationIndicator != null)
            lockedContinuationIndicator.enabled = false;
    }

    private void ShowLockedContinuation(Sprite sprite)
    {
        if (revealedActionIcon != null)
            revealedActionIcon.enabled = false;

        if (emptyIndicator != null)
            emptyIndicator.gameObject.SetActive(false);

        if (lockedContinuationIndicator != null)
        {
            lockedContinuationIndicator.sprite = sprite;
            lockedContinuationIndicator.color = new Color(1f, 1f, 1f, 0.55f);
            lockedContinuationIndicator.enabled = sprite != null;
        }
    }

    private void EnsureRevealLayer()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null)
            return;

        if (sightRevealLayer == null)
        {
            sightRevealLayer = CreateStretchChild("SightRevealLayer", transform).gameObject;
            CanvasGroup group = sightRevealLayer.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        if (revealedActionIcon == null)
        {
            revealedActionIcon = CreateStretchImage("RevealedActionIcon", sightRevealLayer.transform, Color.white);
            revealedActionIcon.preserveAspect = true;
        }

        if (lockedContinuationIndicator == null)
        {
            lockedContinuationIndicator = CreateStretchImage("LockedContinuationIndicator", sightRevealLayer.transform, new Color(1f, 1f, 1f, 0.55f));
            lockedContinuationIndicator.preserveAspect = true;
        }

        if (emptyIndicator == null)
        {
            GameObject obj = new GameObject("EmptyIndicator", typeof(RectTransform));
            obj.transform.SetParent(sightRevealLayer.transform, false);
            RectTransform textRect = obj.GetComponent<RectTransform>();
            SetStretch(textRect);
            emptyIndicator = obj.AddComponent<Text>();
            emptyIndicator.text = "-";
            emptyIndicator.alignment = TextAnchor.MiddleCenter;
            emptyIndicator.color = new Color(0.7f, 0.95f, 1f, 0.9f);
            emptyIndicator.raycastTarget = false;
        }
    }

    private void PlayWatchedSlotPulse()
    {
        ResolveReferences();

        if (watchedPulseCoroutine != null)
            StopCoroutine(watchedPulseCoroutine);

        watchedPulseCoroutine = StartCoroutine(WatchedSlotPulseRoutine());
    }

    private IEnumerator WatchedSlotPulseRoutine()
    {
        RectTransform rect = transform as RectTransform;
        Vector3 baseScale = rect != null ? rect.localScale : Vector3.one;
        float duration = Mathf.Max(0.01f, watchedSlotPulseDuration);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float pulse = t < 0.5f ? t / 0.5f : 1f - ((t - 0.5f) / 0.5f);

            if (rect != null)
                rect.localScale = Vector3.Lerp(baseScale, baseScale * watchedSlotPulseScale, pulse);

            yield return null;
        }

        if (rect != null)
            rect.localScale = baseScale;

        watchedPulseCoroutine = null;
    }

    private Sprite LoadDefaultWatchedSlotSprite()
    {
        for (int i = 0; i < DefaultWatchedSlotSpriteResourcePaths.Length; i++)
        {
            Sprite sprite = LoadFirstSpriteFromResources(DefaultWatchedSlotSpriteResourcePaths[i]);
            if (sprite != null)
                return sprite;
        }

        Debug.LogWarning("SightSlotView: watched slot sprite is missing. Assign sightWatchedSlotSprite or OracleClassConfig.sightSelectedSlotFrame.");
        return null;
    }

    private Sprite LoadFirstSpriteFromResources(string resourcesPath)
    {
        if (string.IsNullOrEmpty(resourcesPath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcesPath);
        if (sprites != null && sprites.Length > 0)
            return sprites[0];

        return null;
    }

    private Image CreateStretchImage(string name, Transform parent, Color color)
    {
        RectTransform child = CreateStretchChild(name, parent);
        Image image = child.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private RectTransform CreateStretchChild(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        SetStretch(rect);
        return rect;
    }

    private void SetStretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private GameObject FindChildByName(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i] != transform && children[i].name == childName)
                return children[i].gameObject;
        }

        return null;
    }

    private Image FindImageByName(params string[] names)
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null || images[i].transform == transform)
                continue;

            for (int n = 0; n < names.Length; n++)
            {
                if (images[i].name == names[n])
                    return images[i];
            }
        }

        return null;
    }

    private Text FindTextByName(params string[] names)
    {
        Text[] texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null)
                continue;

            for (int n = 0; n < names.Length; n++)
            {
                if (texts[i].name == names[n])
                    return texts[i];
            }
        }

        return null;
    }
}
