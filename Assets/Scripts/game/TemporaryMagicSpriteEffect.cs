using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class TemporaryMagicSpriteEffect : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector3 startScale;
    private Vector3 endScale;
    private Color startColor;
    private Color endColor;
    private float duration;
    private float elapsed;
    private float rotationSpeed;
    private bool destroyOnComplete = true;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(
        Vector3 fromScale,
        Vector3 toScale,
        Color fromColor,
        Color toColor,
        float effectDuration,
        float zRotationSpeed,
        bool destroyWhenFinished)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        startScale = fromScale;
        endScale = toScale;
        startColor = fromColor;
        endColor = toColor;
        duration = Mathf.Max(0.01f, effectDuration);
        rotationSpeed = zRotationSpeed;
        destroyOnComplete = destroyWhenFinished;
        elapsed = 0f;

        transform.localScale = startScale;
        if (spriteRenderer != null)
            spriteRenderer.color = startColor;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float smooth = Mathf.SmoothStep(0f, 1f, t);

        transform.localScale = Vector3.Lerp(startScale, endScale, smooth);
        transform.Rotate(0f, 0f, rotationSpeed * Time.unscaledDeltaTime, Space.Self);

        if (spriteRenderer != null)
            spriteRenderer.color = Color.Lerp(startColor, endColor, smooth);

        if (t >= 1f && destroyOnComplete)
            Destroy(gameObject);
    }
}
