using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The background owns the palette. UI previews never register a character-light override.
[DisallowMultipleComponent, DefaultExecutionOrder(-90)]
public sealed class StageReadabilityController : MonoBehaviour, IMaterialModifier
{
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private StageReadabilityPalette palette;
    [SerializeField] private Material backgroundMaterial;
    private Image preview;
    private Material originalMaterial, uiMaterial, uiSource, uiStyle;
    private MaterialPropertyBlock block;
    private Sprite appliedSprite;
    private StageReadabilityPalette.Entry entry;
    private bool applied, registered;
    private int sceneHandle;
    private static readonly int GradeId = Shader.PropertyToID("_StageGrade");
    private static readonly Dictionary<int, StageReadabilityController> stages = new Dictionary<int, StageReadabilityController>();
    public bool HasStyle => isActiveAndEnabled && ResolveEntry(out _);
    public SpriteRenderer BackgroundRenderer => backgroundRenderer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => stages.Clear();

    public static void ForImage(Image image)
    {
        if (image == null) return;
        var controller = image.GetComponent<StageReadabilityController>() ?? image.gameObject.AddComponent<StageReadabilityController>();
        controller.Refresh();
    }

    public static bool TryGetCharacterStyle(GameObject character, out StageReadabilityPalette.Entry value)
    {
        if (stages.TryGetValue(character.scene.handle, out var stage) && stage != null && stage.isActiveAndEnabled)
            return stage.ResolveEntry(out value);
        value = default;
        return false;
    }

    private bool ResolveEntry(out StageReadabilityPalette.Entry value)
    {
        if (palette == null) palette = Resources.Load<StageReadabilityPalette>("Combat/StageReadabilityPalette");
        Sprite sprite = backgroundRenderer != null ? backgroundRenderer.sprite : preview != null ? preview.overrideSprite : null;
        if (palette != null) return palette.TryGet(sprite, out value);
        value = default;
        return false;
    }

    private void OnEnable()
    {
        if (backgroundRenderer == null) backgroundRenderer = GetComponent<SpriteRenderer>();
        preview = GetComponent<Image>();
        if (backgroundRenderer != null)
        {
            sceneHandle = gameObject.scene.handle;
            stages[sceneHandle] = this;
            registered = true;
        }
        Refresh();
    }

    private void LateUpdate() => Refresh();
    public void Refresh()
    {
        if (!isActiveAndEnabled) return;
        if (!ResolveEntry(out entry)) { Restore(); return; }
        Sprite source = backgroundRenderer != null ? backgroundRenderer.sprite : preview.overrideSprite;
        if (backgroundRenderer != null)
        {
            if (backgroundMaterial == null) backgroundMaterial = Resources.Load<Material>("Combat/Materials/StageBackground");
            if (backgroundMaterial == null) return;
            if (!applied) originalMaterial = backgroundRenderer.sharedMaterial;
            backgroundRenderer.sharedMaterial = backgroundMaterial;
            if (block == null) block = new MaterialPropertyBlock();
            backgroundRenderer.GetPropertyBlock(block);
            block.SetVector(GradeId, entry.Grade);
            backgroundRenderer.SetPropertyBlock(block);
        }
        else if (preview != null)
        {
            if (!applied || appliedSprite != source) preview.SetMaterialDirty();
            if (uiMaterial != null) uiMaterial.SetVector(GradeId, entry.Grade);
        }
        applied = true;
        appliedSprite = source;
    }

    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if (!isActiveAndEnabled || preview == null || !ResolveEntry(out entry)) return baseMaterial;
        if (uiStyle == null) uiStyle = Resources.Load<Material>("Combat/Materials/StageBackgroundUI");
        if (uiStyle == null) return baseMaterial;
        if (uiMaterial == null || uiSource != baseMaterial)
        {
            ReleaseUI();
            uiSource = baseMaterial;
            // MaskableGraphic supplies the stencil state before this modifier runs.
            uiMaterial = new Material(baseMaterial) { shader = uiStyle.shader, name = "Stage preview grade", hideFlags = HideFlags.HideAndDontSave };
        }
        uiMaterial.SetVector(GradeId, entry.Grade);
        return uiMaterial;
    }

    private void Restore()
    {
        if (applied && backgroundRenderer != null)
        {
            if (backgroundRenderer.sharedMaterial == backgroundMaterial) backgroundRenderer.sharedMaterial = originalMaterial;
            backgroundRenderer.GetPropertyBlock(block);
            block.SetVector(GradeId, new Vector4(1, 1, 1, 0));
            backgroundRenderer.SetPropertyBlock(block);
        }
        if (applied && preview != null) preview.SetMaterialDirty();
        applied = false;
        appliedSprite = null;
        ReleaseUI();
    }

    private void ReleaseUI()
    {
        if (uiMaterial != null)
        {
            if (Application.isPlaying) Destroy(uiMaterial); else DestroyImmediate(uiMaterial);
        }
        uiMaterial = null;
        uiSource = null;
    }

    private void OnDisable()
    {
        if (registered && stages.TryGetValue(sceneHandle, out var stage) && stage == this) stages.Remove(sceneHandle);
        registered = false;
        Restore();
    }
    private void OnDestroy() => ReleaseUI();
}
