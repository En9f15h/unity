using System.Collections;
using UnityEngine;
using UnityEngine.Sprites;

[DisallowMultipleComponent, DefaultExecutionOrder(100)]
public sealed class CharacterShaderFeedback : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] bodyRenderers;
    [SerializeField] private Material characterMaterial;
    [SerializeField, Range(0.05f, 0.4f)] private float hitDuration = 0.13f;
    [SerializeField, ColorUsage(false, true)] private Color hitColor = new Color(1.5f, 1.1f, 0.8f);
    private static readonly int HitId = Shader.PropertyToID("_HitAmount"), HitColorId = Shader.PropertyToID("_HitColor"),
        PhaseId = Shader.PropertyToID("_Dissolve"), SpaceId = Shader.PropertyToID("_CharacterWorldToLocal"),
        HeightId = Shader.PropertyToID("_CharacterHeight"), UVId = Shader.PropertyToID("_SpriteUVRect"),
        RimId = Shader.PropertyToID("_RimStrength"), RimColorId = Shader.PropertyToID("_RimColor");
    private MaterialPropertyBlock block;
    private Coroutine hitRoutine;
    private float hitAmount, characterHeight = 1f, visibility = 1f;
    private Color activeHitColor;
    public float Visibility => visibility;
    public SpriteRenderer[] BodyRenderers => bodyRenderers;
    private void Awake() => InitializeRenderers();
    private void OnEnable() => ResetPresentation();
    private void OnDisable()
    {
        if (hitRoutine != null) StopCoroutine(hitRoutine);
        hitRoutine = null;
        ResetPresentation();
    }
    public void InitializeRenderers()
    {
        if (block == null) block = new MaterialPropertyBlock();
        if (bodyRenderers == null) return;
        bool hasBounds = false;
        Bounds combined = default;
        foreach (SpriteRenderer renderer in bodyRenderers)
        {
            if (renderer == null) continue;
            if (characterMaterial != null) renderer.sharedMaterial = characterMaterial;
            if (!hasBounds) { combined = renderer.bounds; hasBounds = true; }
            else combined.Encapsulate(renderer.bounds);
        }
        if (hasBounds) characterHeight = Mathf.Max(0.1f, combined.size.y / Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y)));
        ApplyProperties();
    }
    public void FlashHit()
    {
        FlashHit(hitDuration, 0.72f, hitColor);
    }
    public void FlashHit(float seconds, float strength, Color color)
    {
        if (!isActiveAndEnabled) return;
        if (hitRoutine != null) StopCoroutine(hitRoutine);
        activeHitColor = color;
        hitRoutine = StartCoroutine(HitFlash(Mathf.Max(0.01f, seconds), Mathf.Clamp01(strength)));
    }
    private IEnumerator HitFlash(float seconds, float strength)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            hitAmount = strength * (1f - Mathf.Clamp01(elapsed / seconds));
            ApplyProperties();
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        hitAmount = 0f; hitRoutine = null;
        ApplyProperties();
    }
    public void SetPhaseVisibility(float value) { visibility = Mathf.Clamp01(value); ApplyProperties(); }
    public void ResetPresentation() { hitAmount = 0f; visibility = 1f; ApplyProperties(); }
    private void LateUpdate() => ApplyProperties();
    private void ApplyProperties()
    {
        if (bodyRenderers == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        Matrix4x4 space = transform.worldToLocalMatrix;
        bool stageStyled = StageReadabilityController.TryGetCharacterStyle(gameObject, out var stage) && isActiveAndEnabled;
        foreach (SpriteRenderer renderer in bodyRenderers)
        {
            if (renderer == null || renderer.sprite == null) continue;
            // Keep other property-block values, including SpriteSkin data.
            renderer.GetPropertyBlock(block);
            block.SetFloat(HitId, hitAmount); block.SetColor(HitColorId, activeHitColor);
            block.SetFloat(PhaseId, 1f - visibility); block.SetFloat(HeightId, characterHeight);
            block.SetMatrix(SpaceId, space);
            block.SetVector(UVId, DataUtility.GetOuterUV(renderer.sprite));
            var material = renderer.sharedMaterial;
            if (material != null && material.HasProperty(RimId))
            {
                // Scale each skin's authored edge strength; sketch skins retain their restrained value.
                float strength = material.GetFloat(RimId);
                Color edgeColor = material.GetColor(RimColorId);
                block.SetFloat(RimId, stageStyled ? strength * stage.edgeMultiplier : strength);
                block.SetColor(RimColorId, stageStyled ? Color.Lerp(edgeColor, stage.edgeColor, stage.edgeColorMix) : edgeColor);
            }
            renderer.SetPropertyBlock(block);
        }
    }
}
