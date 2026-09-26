using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterRadarChartController : MonoBehaviour
{
    [SerializeField] private HexRadarChartGraphic chart;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite fallbackBackgroundSprite;
    [SerializeField] private TMP_Text[] axisLabels;
    [SerializeField] private Image[] axisIcons;
    [SerializeField] private Animator animator;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool showAxisLabels;
    [SerializeField] private bool syncBackgroundRectAtRuntime;
    [SerializeField] private bool allowRuntimeBackgroundSpriteOverrides;

    private static readonly string[] DefaultLabels =
    {
        "Vitality",
        "Attack",
        "Defense",
        "Mobility",
        "Range",
        "Prediction"
    };

    private void Awake()
    {
        ResolveReferences();
        ApplyLabels();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!Application.isPlaying || syncBackgroundRectAtRuntime)
            SyncBackgroundRect();
    }

    public void ApplyDefinition(CharacterSelectionDefinition definition, bool visible, bool playReveal)
    {
        ResolveReferences();

        if (definition == null || !visible)
        {
            Clear();
            return;
        }

        if (chart != null)
        {
            chart.SetChartVisible(true);
            chart.SetValues(definition.GetRadarValues());
        }

        ApplyBackground(definition, true);
        ApplyLabels();

        if (!playReveal)
            ShowImmediately();

        if (playReveal)
        {
            EnableAnimator();
            PlayTrigger("RadarReveal");
        }
    }

    public void SetValues(IReadOnlyList<float> values)
    {
        ResolveReferences();
        if (chart != null)
            chart.SetValues(values);
    }

    public void Clear()
    {
        ResolveReferences();
        EnableAnimator();
        if (chart != null)
        {
            chart.ClearValues();
            chart.SetChartVisible(false);
        }

        ApplyBackground(null, false);
        PlayTrigger("RadarHide");
    }

    public void SetVisible(bool visible)
    {
        ResolveReferences();
        if (chart != null)
            chart.SetChartVisible(visible);

        ApplyBackground(null, visible);

        if (visible)
            ShowImmediately();
        else
            HideImmediately();
    }

    public void PlayReveal()
    {
        ResolveReferences();
        EnableAnimator();
        if (chart != null)
            chart.SetChartVisible(true);

        ApplyBackground(null, true);
        PlayTrigger("RadarReveal");
    }

    private void ResolveReferences()
    {
        if (chart == null)
            chart = GetComponentInChildren<HexRadarChartGraphic>(true);

        if (backgroundImage == null)
            backgroundImage = FindBackgroundImage();

        if (backgroundImage != null)
        {
            if (fallbackBackgroundSprite == null && backgroundImage.sprite != null)
                fallbackBackgroundSprite = backgroundImage.sprite;

            backgroundImage.raycastTarget = false;
            backgroundImage.preserveAspect = true;
            if (!Application.isPlaying || syncBackgroundRectAtRuntime)
                SyncBackgroundRect();
        }

        if (animator == null)
            animator = GetComponent<Animator>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private Image FindBackgroundImage()
    {
        Transform child = transform.Find("RadarBackgroundImage");
        if (child != null && child.TryGetComponent(out Image childImage))
            return childImage;

        if (transform.parent != null)
        {
            Transform sibling = transform.parent.Find("RadarBackgroundImage");
            if (sibling != null && sibling.TryGetComponent(out Image siblingImage))
                return siblingImage;
        }

        return null;
    }

    private void ApplyBackground(CharacterSelectionDefinition definition, bool visible)
    {
        if (backgroundImage == null)
            return;

        Sprite sprite = definition != null && definition.radarBackgroundSprite != null
            ? definition.radarBackgroundSprite
            : fallbackBackgroundSprite;

        if (allowRuntimeBackgroundSpriteOverrides)
            backgroundImage.sprite = sprite;

        Sprite visibleSprite = backgroundImage.sprite;

        backgroundImage.enabled = visible && visibleSprite != null;
        backgroundImage.gameObject.SetActive(visible && visibleSprite != null);
        if (!Application.isPlaying || syncBackgroundRectAtRuntime)
            SyncBackgroundRect();
    }

    private void SyncBackgroundRect()
    {
        if (backgroundImage == null)
            return;

        RectTransform chartRect = transform as RectTransform;
        RectTransform backgroundRect = backgroundImage.transform as RectTransform;
        if (chartRect == null || backgroundRect == null)
            return;

        backgroundRect.anchorMin = chartRect.anchorMin;
        backgroundRect.anchorMax = chartRect.anchorMax;
        backgroundRect.pivot = chartRect.pivot;
        backgroundRect.anchoredPosition = chartRect.anchoredPosition;
        backgroundRect.sizeDelta = chartRect.sizeDelta;
        backgroundRect.localScale = Vector3.one;

        if (backgroundRect.parent == chartRect.parent)
        {
            int chartIndex = chartRect.GetSiblingIndex();
            int backgroundIndex = backgroundRect.GetSiblingIndex();
            int targetIndex = backgroundIndex < chartIndex ? Mathf.Max(0, chartIndex - 1) : chartIndex;
            backgroundRect.SetSiblingIndex(targetIndex);
        }
    }

    private void ApplyLabels()
    {
        if (axisLabels == null)
            axisLabels = new TMP_Text[0];

        for (int i = 0; i < axisLabels.Length && i < DefaultLabels.Length; i++)
        {
            if (axisLabels[i] != null)
            {
                axisLabels[i].text = showAxisLabels ? DefaultLabels[i] : string.Empty;
                axisLabels[i].gameObject.SetActive(showAxisLabels);
            }
        }

        if (axisIcons == null)
            return;

        for (int i = 0; i < axisIcons.Length; i++)
        {
            if (axisIcons[i] == null)
                continue;

            axisIcons[i].gameObject.SetActive(axisIcons[i].sprite != null);
            axisIcons[i].raycastTarget = false;
            axisIcons[i].preserveAspect = true;
        }
    }

    private void ShowImmediately()
    {
        if (animator != null)
            animator.enabled = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        transform.localScale = Vector3.one;
    }

    private void HideImmediately()
    {
        if (animator != null)
            animator.enabled = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        transform.localScale = Vector3.one;
    }

    private void EnableAnimator()
    {
        if (animator != null && !animator.enabled)
            animator.enabled = true;
    }

    private void PlayTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName) || !HasTrigger(animator, triggerName))
            return;

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
    }

    private bool HasTrigger(Animator targetAnimator, string triggerName)
    {
        AnimatorControllerParameter[] parameters = targetAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].name == triggerName)
                return true;
        }

        return false;
    }
}
