using UnityEngine;

// Demo-only component. No animation, game state or shared material is modified.
[ExecuteAlways]
public sealed class ShaderGraphWorkshopPreview : MonoBehaviour
{
    public SpriteRenderer[] dissolveSprites;
    [Range(0, 1)] public float middleProgress = .5f;
    public bool animateMiddle;
    [Min(.1f)] public float previewCycleSeconds = 2;
    private MaterialPropertyBlock block;
    private void OnEnable() => Apply();
    private void OnValidate() => Apply();
    private void Update() => Apply();
    private void Apply()
    {
        if (dissolveSprites == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        for (int i = 0; i < dissolveSprites.Length; i++)
        {
            var sprite = dissolveSprites[i]; if (sprite == null) continue;
            float progress = i == 0 ? 0 : i == 2 ? 1 : middleProgress;
            if (i == 1 && animateMiddle && Application.isPlaying)
                progress = Mathf.PingPong(Time.unscaledTime / Mathf.Max(.1f, previewCycleSeconds), 1);
            sprite.GetPropertyBlock(block); block.SetFloat("_EffectProgress", progress); sprite.SetPropertyBlock(block);
        }
    }
}
