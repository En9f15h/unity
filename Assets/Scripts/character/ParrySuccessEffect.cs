using System.Collections;
using UnityEngine;

public class ParrySuccessEffect : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private float lifeTime = 0.6f;
    [SerializeField] private float fadeOutDuration = 0.18f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Visual Impact")]
    [SerializeField] private float pulseScale = 1.22f;
    [SerializeField] private float pulseInDuration = 0.05f;
    [SerializeField] private float pulseOutDuration = 0.16f;

    private ParticleSystem[] particleSystems;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Vector3 originalScale;
    private Coroutine playCoroutine;

    private void Awake()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[spriteRenderers.Length];
        originalScale = transform.localScale;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                originalColors[i] = spriteRenderers[i].color;
        }
    }

    private void OnEnable()
    {
        RestoreOriginalColors();
        transform.localScale = originalScale;

        if (playCoroutine != null)
            StopCoroutine(playCoroutine);

        playCoroutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        PlayParticles();

        yield return StartCoroutine(PlayPulse());

        float holdTime = Mathf.Max(0f, lifeTime - fadeOutDuration - pulseInDuration - pulseOutDuration);
        yield return WaitSeconds(holdTime);
        yield return StartCoroutine(FadeOut());

        Destroy(gameObject);
    }

    private IEnumerator PlayPulse()
    {
        if (pulseScale <= 0f)
            yield break;

        Vector3 peakScale = originalScale * pulseScale;
        yield return ScaleOverTime(originalScale, peakScale, pulseInDuration);
        yield return ScaleOverTime(peakScale, originalScale, pulseOutDuration);
    }

    private IEnumerator ScaleOverTime(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            transform.localScale = to;
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += GetDeltaTime();
            float t = Mathf.Clamp01(timer / duration);
            transform.localScale = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        transform.localScale = to;
    }

    private IEnumerator FadeOut()
    {
        if (fadeOutDuration <= 0f || spriteRenderers == null || spriteRenderers.Length == 0)
            yield break;

        float timer = 0f;
        while (timer < fadeOutDuration)
        {
            timer += GetDeltaTime();
            float alphaMul = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / fadeOutDuration));

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

    private void PlayParticles()
    {
        if (particleSystems == null)
            return;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null)
                continue;

            particleSystems[i].gameObject.SetActive(true);
            particleSystems[i].Clear(true);
            particleSystems[i].Play(true);
        }
    }

    private IEnumerator WaitSeconds(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        float timer = 0f;
        while (timer < seconds)
        {
            timer += GetDeltaTime();
            yield return null;
        }
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
