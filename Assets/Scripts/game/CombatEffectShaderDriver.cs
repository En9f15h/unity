using UnityEngine;
using UnityEngine.Sprites;

[DisallowMultipleComponent]
public sealed class CombatEffectShaderDriver : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float duration = 0.6f;
    [Tooltip("Fraction of the supplied effect lifetime at which the Graph finishes dissolving. Fade uses 0.5 to finish at its return beat.")]
    [SerializeField, Range(0.05f, 1f)] private float graphDissolveEnd = 1f;
    public const string FlowGraphShader = "Combat/Graphs/EnergyFlow";
    public const string DissolveGraphShader = "Combat/Graphs/EdgeDissolve";
    private SpriteRenderer[] sprites;
    private MaterialPropertyBlock block;
    private float startTime, impactTime = -10000f;
    private Vector3 impactPosition;
    private bool hasImpact;
    private static readonly int UVId = Shader.PropertyToID("_SpriteUVRect"), ProgressId = Shader.PropertyToID("_EffectProgress"),
        ImpactId = Shader.PropertyToID("_ImpactStart"), ImpactUVId = Shader.PropertyToID("_ImpactUV");
    private void OnEnable() => Play(duration);
    public void Play(float seconds)
    {
        duration = Mathf.Max(0.05f, seconds); startTime = Time.unscaledTime;
        hasImpact = false; impactTime = -10000f;
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        ApplyProperties(0f);
    }
    public void PulseImpact(Vector3 worldPosition)
    {
        hasImpact = true; impactPosition = worldPosition; impactTime = Time.unscaledTime;
        ApplyProperties(Mathf.Clamp01((Time.unscaledTime - startTime) / duration));
    }
    private void LateUpdate() => ApplyProperties(Mathf.Clamp01((Time.unscaledTime - startTime) / duration));
    public void ApplyProperties(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (sprites == null) sprites = GetComponentsInChildren<SpriteRenderer>(true);
        if (block == null) block = new MaterialPropertyBlock();
        foreach (SpriteRenderer sprite in sprites)
        {
            if (sprite == null || sprite.sprite == null || sprite.sharedMaterial == null) continue;
            string shaderName = sprite.sharedMaterial.shader.name;
            if (shaderName == DissolveGraphShader)
            {
                sprite.GetPropertyBlock(block);
                block.SetFloat(ProgressId, Mathf.Clamp01(progress / Mathf.Max(0.05f, graphDissolveEnd)));
                sprite.SetPropertyBlock(block);
                continue;
            }
            // EnergyFlow uses the existing global clock. Ward, ghost and impact properties
            // remain exclusive to the original shader so their authored behaviour is preserved.
            if (shaderName != "Combat/Energy Sprite") continue;
            sprite.GetPropertyBlock(block);
            block.SetVector(UVId, DataUtility.GetOuterUV(sprite.sprite));
            block.SetFloat(ProgressId, progress); block.SetFloat(ImpactId, impactTime);
            Vector2 hitUV = new Vector2(0.5f, 0.5f);
            if (hasImpact)
            {
                Vector3 local = sprite.transform.InverseTransformPoint(impactPosition);
                if (sprite.flipX) local.x = -local.x;
                if (sprite.flipY) local.y = -local.y;
                Bounds bounds = sprite.sprite.bounds;
                hitUV = new Vector2(Mathf.InverseLerp(bounds.min.x, bounds.max.x, local.x), Mathf.InverseLerp(bounds.min.y, bounds.max.y, local.y));
            }
            block.SetVector(ImpactUVId, new Vector4(hitUV.x, hitUV.y, 0, 0));
            sprite.SetPropertyBlock(block);
        }
    }
}
