using System.Collections;
using UnityEngine;

public class BloodDecalFade : MonoBehaviour
{
    [Header("Random Blood Decal Sprites")]
    [SerializeField] private Sprite[] randomSprites;

    [Header("Target SpriteRenderer")]
    [SerializeField] private SpriteRenderer mainSpriteRenderer;

    [Header("Hold Duration")]
    [SerializeField] private float holdTime = 4f;

    [Header("Fade Duration")]
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Spawn Variation")]
    [SerializeField] private float settleScale = 1.08f;
    [SerializeField] private float settleDuration = 0.12f;

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Vector3 originalScale;
    private Coroutine fadeCoroutine;
    private MaterialPropertyBlock shaderBlock;

    private void Awake()
    {
        if (mainSpriteRenderer == null)
            mainSpriteRenderer = GetComponent<SpriteRenderer>();

        if (mainSpriteRenderer == null)
            mainSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[spriteRenderers.Length];
        originalScale = transform.localScale;

        CacheOriginalColors();
    }

    private void OnEnable()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            originalColors = new Color[spriteRenderers.Length];
        }

        CacheOriginalColors();
        RestoreOriginalColors();
        ApplyRandomSprite();
        ApplyShaderState(0f, 0f);

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeRoutine());
    }

    private void OnDisable()
    {
        RestoreOriginalColors();
        ApplyShaderState(0f, 0f);
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }

    public void RestartFadeFromCurrentTransform()
    {
        originalScale = transform.localScale;
        CacheOriginalColors();
        RestoreOriginalColors();
        ApplyShaderState(0f, 0f);

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeRoutine());
    }

    private void CacheOriginalColors()
    {
        if (spriteRenderers == null || originalColors == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                originalColors[i] = spriteRenderers[i].color;
        }
    }

    private void RestoreOriginalColors()
    {
        if (spriteRenderers == null || originalColors == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null && i < originalColors.Length)
                spriteRenderers[i].color = originalColors[i];
        }
    }

    private void ApplyRandomSprite()
    {
        if (mainSpriteRenderer == null)
            return;

        if (randomSprites == null || randomSprites.Length == 0)
            return;

        int index = Random.Range(0, randomSprites.Length);
        if (randomSprites[index] != null)
            mainSpriteRenderer.sprite = randomSprites[index];
    }

    private IEnumerator FadeRoutine()
    {
        yield return StartCoroutine(PlaySettlePulse());
        float drying = 0f;
        while (drying < holdTime)
        {
            drying += GetDeltaTime();
            ApplyShaderState(Mathf.Clamp01(drying / Mathf.Max(0.01f, holdTime)), 0f);
            yield return null;
        }

        float t = 0f;
        fadeDuration = Mathf.Max(0.01f, fadeDuration);

        while (t < fadeDuration)
        {
            t += GetDeltaTime();
            float normalized = Mathf.Clamp01(t / fadeDuration);
            float alphaMul = 1f - Mathf.SmoothStep(0f, 1f, normalized);
            ApplyShaderState(1f, normalized);

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == null || i >= originalColors.Length)
                    continue;

                Color c = originalColors[i];
                c.a = originalColors[i].a * alphaMul;
                spriteRenderers[i].color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator PlaySettlePulse()
    {
        if (settleDuration <= 0f || Mathf.Approximately(settleScale, 1f))
            yield break;

        float t = 0f;
        Vector3 startScale = originalScale * Mathf.Max(0.01f, settleScale);
        transform.localScale = startScale;

        while (t < settleDuration)
        {
            t += GetDeltaTime();
            float normalized = Mathf.Clamp01(t / settleDuration);
            transform.localScale = Vector3.Lerp(startScale, originalScale, Mathf.SmoothStep(0f, 1f, normalized));
            yield return null;
        }

        transform.localScale = originalScale;
    }

    private IEnumerator WaitRealtimeOrScaled(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        float t = 0f;
        while (t < seconds)
        {
            t += GetDeltaTime();
            yield return null;
        }
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    public void ApplyShaderState(float dryness, float erosion)
    {
        if (spriteRenderers == null) spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (shaderBlock == null) shaderBlock = new MaterialPropertyBlock();
        foreach (SpriteRenderer renderer in spriteRenderers)
        {
            if (renderer == null || renderer.sprite == null) continue;
            renderer.GetPropertyBlock(shaderBlock);
            shaderBlock.SetFloat("_Dryness", Mathf.Clamp01(dryness));
            shaderBlock.SetFloat("_Erosion", Mathf.Clamp01(erosion));
            shaderBlock.SetVector("_SpriteUVRect", UnityEngine.Sprites.DataUtility.GetOuterUV(renderer.sprite));
            renderer.SetPropertyBlock(shaderBlock);
        }
    }
}
