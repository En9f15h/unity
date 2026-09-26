using UnityEngine;
using UnityEngine.UI;

public class CharacterPreviewController : MonoBehaviour
{
    private const string DefaultHiddenPortraitResourcePath = "CharacterSelection/HiddenCharacter";

    [SerializeField] private Transform previewRoot;
    [SerializeField] private Image portraitFallbackImage;
    [SerializeField] private Sprite hiddenPortraitSprite;
    [SerializeField] private string hiddenPortraitResourcePath = DefaultHiddenPortraitResourcePath;
    [SerializeField] private GameObject hiddenSilhouette;
    [SerializeField] private Vector3 previewScale = Vector3.one;
    [SerializeField] private bool fitPrefabToPreviewRoot = true;
    [SerializeField] [Range(0.1f, 1f)] private float previewRootFill = 0.86f;
    [SerializeField] private Vector2 previewRootOffset;

    private GameObject currentPreview;
    private Animator currentAnimator;
    private int facingDirection = 1;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (currentPreview != null)
            FitPreviewToRoot();
    }

    public void ShowCharacter(CharacterSelectionDefinition definition, int skinIndex)
    {
        ResolveReferences();
        ClearPreview();

        if (definition == null)
        {
            ShowHiddenSilhouette();
            return;
        }

        GameObject prefab = definition.GetSkinPreviewPrefab(skinIndex);
        if (prefab != null && previewRoot != null)
        {
            currentPreview = Instantiate(prefab, previewRoot);
            currentPreview.transform.localPosition = Vector3.zero;
            currentPreview.transform.localRotation = Quaternion.identity;
            DisablePreviewPhysics(currentPreview);
            ApplyFacingScale();
            FitPreviewToRoot();
            currentAnimator = currentPreview.GetComponentInChildren<Animator>(true);

            if (portraitFallbackImage != null)
                portraitFallbackImage.gameObject.SetActive(false);

            if (hiddenSilhouette != null)
                hiddenSilhouette.SetActive(false);

            PlayIdle();
            return;
        }

        ShowPortrait(definition.GetSkinPortrait(skinIndex));
    }

    public void ShowPortrait(Sprite sprite)
    {
        ResolveReferences();
        ClearPreview();

        if (portraitFallbackImage != null)
        {
            portraitFallbackImage.sprite = sprite;
            portraitFallbackImage.enabled = sprite != null;
            portraitFallbackImage.gameObject.SetActive(sprite != null);
        }

        if (hiddenSilhouette != null)
            hiddenSilhouette.SetActive(false);

        if (sprite == null)
            ShowHiddenSilhouette();
    }

    public void ShowHiddenSilhouette()
    {
        ResolveReferences();
        ClearPreview();

        Sprite hiddenSprite = ResolveHiddenPortraitSprite();
        bool canShowHiddenPortrait = hiddenSprite != null && portraitFallbackImage != null;
        if (portraitFallbackImage != null)
        {
            portraitFallbackImage.sprite = hiddenSprite;
            portraitFallbackImage.enabled = canShowHiddenPortrait;
            portraitFallbackImage.preserveAspect = true;
            portraitFallbackImage.gameObject.SetActive(canShowHiddenPortrait);
        }

        if (hiddenSilhouette != null)
            hiddenSilhouette.SetActive(!canShowHiddenPortrait);
    }

    public void SetFacingDirection(int direction)
    {
        facingDirection = direction < 0 ? -1 : 1;
        ApplyFacingScale();
    }

    public void PlayIdle()
    {
        PlayTrigger("Idle");
    }

    public void PlayReveal()
    {
        PlayTrigger("Reveal");
    }

    public void PlayReady()
    {
        PlayTrigger("Ready");
    }

    public void ClearPreview()
    {
        if (currentPreview != null)
            Destroy(currentPreview);

        currentPreview = null;
        currentAnimator = null;
    }

    private void ResolveReferences()
    {
        if (previewRoot == null)
            previewRoot = transform;

        if (portraitFallbackImage == null)
        {
            Transform portrait = FindChildRecursive(transform, "PortraitFallback");
            if (portrait != null)
                portraitFallbackImage = portrait.GetComponent<Image>();
        }
    }

    private Sprite ResolveHiddenPortraitSprite()
    {
        if (hiddenPortraitSprite != null)
            return hiddenPortraitSprite;

        string path = string.IsNullOrEmpty(hiddenPortraitResourcePath) ? DefaultHiddenPortraitResourcePath : hiddenPortraitResourcePath;
        hiddenPortraitSprite = Resources.Load<Sprite>(path);
        return hiddenPortraitSprite;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void ApplyFacingScale()
    {
        if (currentPreview == null)
            return;

        Vector3 scale = previewScale;
        scale.x = Mathf.Abs(scale.x) * facingDirection;
        currentPreview.transform.localScale = scale;
    }

    private void FitPreviewToRoot()
    {
        if (!fitPrefabToPreviewRoot || currentPreview == null || previewRoot == null)
            return;

        RectTransform rootRect = previewRoot as RectTransform;
        if (rootRect == null)
            return;

        RectTransform previewRect = currentPreview.transform as RectTransform;
        if (previewRect != null)
        {
            FitUiPreviewToRoot(previewRect, rootRect);
            return;
        }

        if (!TryGetRendererBounds(currentPreview, out Bounds bounds))
            return;

        Vector2 targetSize = GetWorldRectSize(rootRect) * Mathf.Clamp01(previewRootFill);
        if (targetSize.x <= 0.001f || targetSize.y <= 0.001f || bounds.size.x <= 0.001f || bounds.size.y <= 0.001f)
            return;

        float scaleFactor = Mathf.Min(targetSize.x / bounds.size.x, targetSize.y / bounds.size.y);
        if (float.IsNaN(scaleFactor) || float.IsInfinity(scaleFactor) || scaleFactor <= 0f)
            return;

        Vector3 scale = currentPreview.transform.localScale;
        currentPreview.transform.localScale = new Vector3(scale.x * scaleFactor, scale.y * scaleFactor, scale.z * scaleFactor);

        if (!TryGetRendererBounds(currentPreview, out bounds))
            return;

        Vector3 targetCenter = GetWorldRectCenter(rootRect) + rootRect.TransformVector(new Vector3(previewRootOffset.x, previewRootOffset.y, 0f));
        Vector3 offset = targetCenter - bounds.center;
        offset.z = 0f;
        currentPreview.transform.position += offset;
    }

    private void FitUiPreviewToRoot(RectTransform previewRect, RectTransform rootRect)
    {
        previewRect.anchorMin = new Vector2(0.5f, 0.5f);
        previewRect.anchorMax = new Vector2(0.5f, 0.5f);
        previewRect.pivot = new Vector2(0.5f, 0.5f);
        previewRect.anchoredPosition = previewRootOffset;

        Vector2 rootSize = rootRect.rect.size;
        float fill = Mathf.Clamp01(previewRootFill);
        previewRect.sizeDelta = new Vector2(Mathf.Max(1f, rootSize.x * fill), Mathf.Max(1f, rootSize.y * fill));

        Image[] images = currentPreview.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null)
                continue;

            images[i].raycastTarget = false;
            images[i].preserveAspect = true;
        }
    }

    private void DisablePreviewPhysics(GameObject preview)
    {
        if (preview == null)
            return;

        Rigidbody2D[] bodies2D = preview.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < bodies2D.Length; i++)
        {
            Rigidbody2D body = bodies2D[i];
            if (body == null)
                continue;

            body.gravityScale = 0f;
            body.simulated = false;
        }

        Rigidbody[] bodies3D = preview.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies3D.Length; i++)
        {
            Rigidbody body = bodies3D[i];
            if (body == null)
                continue;

            body.useGravity = false;
            body.isKinematic = true;
        }
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static Vector2 GetWorldRectSize(RectTransform rect)
    {
        Vector3[] corners = GetWorldCorners(rect);
        return new Vector2(Vector3.Distance(corners[0], corners[3]), Vector3.Distance(corners[0], corners[1]));
    }

    private static Vector3 GetWorldRectCenter(RectTransform rect)
    {
        Vector3[] corners = GetWorldCorners(rect);
        return (corners[0] + corners[2]) * 0.5f;
    }

    private static Vector3[] GetWorldCorners(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners;
    }

    private void PlayTrigger(string triggerName)
    {
        if (currentAnimator == null || !HasTrigger(triggerName))
            return;

        currentAnimator.ResetTrigger(triggerName);
        currentAnimator.SetTrigger(triggerName);
    }

    private bool HasTrigger(string triggerName)
    {
        AnimatorControllerParameter[] parameters = currentAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].name == triggerName)
                return true;
        }

        return false;
    }
}
