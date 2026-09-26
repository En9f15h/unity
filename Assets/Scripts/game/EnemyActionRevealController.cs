using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyActionRevealController : MonoBehaviour
{
    [Header("Reveal Animation")]
    [SerializeField, Range(0.05f, 0.5f)] private float revealDuration = 0.18f;
    [SerializeField] private Vector3 startScale = new Vector3(0.92f, 0.92f, 1f);
    [SerializeField] private Vector3 overshootScale = new Vector3(1.03f, 1.03f, 1f);

    [Header("Visual State")]
    [SerializeField] private Color revealedColor = Color.white;
    [SerializeField] private Color lockedContinuationColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color currentHighlightColor = new Color(1f, 0.82f, 0.18f, 0.95f);
    [SerializeField] private float highlightThickness = 2f;

    private readonly List<Image> slotImages = new List<Image>();
    private readonly HashSet<int> revealedLogicalSlots = new HashSet<int>();
    private readonly Dictionary<Image, Coroutine> runningAnimations = new Dictionary<Image, Coroutine>();
    private readonly Dictionary<Image, GameObject> highlightFrames = new Dictionary<Image, GameObject>();

    public void Bind(IReadOnlyList<Image> images)
    {
        slotImages.Clear();

        if (images == null)
            return;

        for (int i = 0; i < images.Count; i++)
        {
            if (images[i] == null)
                continue;

            slotImages.Add(images[i]);
            EnsureHighlightFrame(images[i]);
        }
    }

    public void ResetForPlanning(Sprite hiddenSprite)
    {
        revealedLogicalSlots.Clear();
        CompleteCurrentReveal();

        for (int i = 0; i < slotImages.Count; i++)
            SetHidden(slotImages[i], hiddenSprite);
    }

    public void PrepareForResolution(Sprite hiddenSprite, int preservedLogicalIndex, int preservedVisualIndex)
    {
        revealedLogicalSlots.Clear();
        CompleteCurrentReveal();

        for (int i = 0; i < slotImages.Count; i++)
        {
            if (i == preservedVisualIndex)
                continue;

            SetHidden(slotImages[i], hiddenSprite);
        }

        if (preservedLogicalIndex >= 0)
            revealedLogicalSlots.Add(preservedLogicalIndex);
    }

    public bool IsLogicalSlotRevealed(int logicalSlotIndex)
    {
        return revealedLogicalSlots.Contains(logicalSlotIndex);
    }

    public void RevealSlot(
        int logicalSlotIndex,
        int visualSlotIndex,
        Sprite sprite,
        Sprite fallbackSprite,
        bool lockedContinuation,
        bool animate)
    {
        if (visualSlotIndex < 0 || visualSlotIndex >= slotImages.Count)
            return;

        Image image = slotImages[visualSlotIndex];
        if (image == null)
            return;

        bool alreadyRevealed = revealedLogicalSlots.Contains(logicalSlotIndex);
        revealedLogicalSlots.Add(logicalSlotIndex);

        image.sprite = sprite != null ? sprite : fallbackSprite;
        image.color = lockedContinuation ? lockedContinuationColor : revealedColor;
        if (animate && !alreadyRevealed)
            UIShaderFeedback.Ensure(image, "UI_Rune").Pulse(revealDuration);

        ShowHighlight(image);

        if (animate && !alreadyRevealed)
            PlayRevealAnimation(image);
    }

    public void HighlightSlot(int visualSlotIndex)
    {
        if (visualSlotIndex < 0 || visualSlotIndex >= slotImages.Count)
            return;

        ShowHighlight(slotImages[visualSlotIndex]);
    }

    public void CompleteCurrentReveal()
    {
        foreach (KeyValuePair<Image, GameObject> entry in highlightFrames)
        {
            if (entry.Value != null)
                entry.Value.SetActive(false);
        }
    }

    private void SetHidden(Image image, Sprite hiddenSprite)
    {
        if (image == null)
            return;

        StopRevealAnimation(image);

        image.sprite = hiddenSprite;
        image.color = Color.white;
        image.GetComponent<UIShaderFeedback>()?.ResetVisuals();

        CanvasGroup group = image.GetComponent<CanvasGroup>();
        if (group != null)
            group.alpha = 1f;

        RectTransform rect = image.transform as RectTransform;
        if (rect != null)
            rect.localScale = Vector3.one;

        if (highlightFrames.TryGetValue(image, out GameObject frame) && frame != null)
            frame.SetActive(false);
    }

    private void PlayRevealAnimation(Image image)
    {
        StopRevealAnimation(image);
        runningAnimations[image] = StartCoroutine(RevealAnimationCoroutine(image));
    }

    private IEnumerator RevealAnimationCoroutine(Image image)
    {
        if (image == null)
            yield break;

        CanvasGroup group = image.GetComponent<CanvasGroup>();
        if (group == null)
            group = image.gameObject.AddComponent<CanvasGroup>();

        RectTransform rect = image.transform as RectTransform;
        float duration = Mathf.Max(0.01f, revealDuration);
        float timer = 0f;

        group.alpha = 0f;
        if (rect != null)
            rect.localScale = startScale;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);

            group.alpha = t;

            if (rect != null)
            {
                if (t < 0.65f)
                    rect.localScale = Vector3.Lerp(startScale, overshootScale, t / 0.65f);
                else
                    rect.localScale = Vector3.Lerp(overshootScale, Vector3.one, (t - 0.65f) / 0.35f);
            }

            yield return null;
        }

        group.alpha = 1f;
        if (rect != null)
            rect.localScale = Vector3.one;

        runningAnimations.Remove(image);
    }

    private void StopRevealAnimation(Image image)
    {
        if (image == null)
            return;

        if (!runningAnimations.TryGetValue(image, out Coroutine coroutine) || coroutine == null)
            return;

        StopCoroutine(coroutine);
        runningAnimations.Remove(image);
    }

    private void ShowHighlight(Image image)
    {
        if (image == null)
            return;

        if (!highlightFrames.TryGetValue(image, out GameObject frame) || frame == null)
            frame = EnsureHighlightFrame(image);

        if (frame != null)
        {
            frame.SetActive(true);
            foreach (UIShaderFeedback feedback in frame.GetComponentsInChildren<UIShaderFeedback>())
                feedback.SetState(1f, true);
        }
    }

    private GameObject EnsureHighlightFrame(Image image)
    {
        if (image == null)
            return null;

        if (highlightFrames.TryGetValue(image, out GameObject existing) && existing != null)
            return existing;

        GameObject frame = new GameObject("CurrentRevealHighlight", typeof(RectTransform));
        frame.transform.SetParent(image.transform, false);
        RectTransform rect = frame.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        CreateBar(frame.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, highlightThickness));
        CreateBar(frame.transform, "Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, highlightThickness));
        CreateBar(frame.transform, "Left", Vector2.zero, new Vector2(0f, 1f), new Vector2(highlightThickness, 0f));
        CreateBar(frame.transform, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(highlightThickness, 0f));

        frame.SetActive(false);
        highlightFrames[image] = frame;
        return frame;
    }

    private void CreateBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image image = obj.AddComponent<Image>();
        image.color = currentHighlightColor;
        image.raycastTarget = false;
        UIShaderFeedback.Ensure(image, "UI_Rune").SetState(1f, true);
    }
}
