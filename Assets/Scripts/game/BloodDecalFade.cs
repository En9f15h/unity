using System.Collections;
using UnityEngine;

public class BloodDecalFade : MonoBehaviour
{
    [Header("隨機血跡 Sprite")]
    [SerializeField] private Sprite[] randomSprites;

    [Header("要換圖的主 SpriteRenderer（不指定就自動抓自己身上第一個）")]
    [SerializeField] private SpriteRenderer mainSpriteRenderer;

    [Header("停留時間")]
    [SerializeField] private float holdTime = 4f;

    [Header("淡出時間")]
    [SerializeField] private float fadeDuration = 1.5f;

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;

    private void Awake()
    {
        if (mainSpriteRenderer == null)
            mainSpriteRenderer = GetComponent<SpriteRenderer>();

        if (mainSpriteRenderer == null)
            mainSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                originalColors[i] = spriteRenderers[i].color;
        }
    }

    private void OnEnable()
    {
        ApplyRandomSprite();
        StartCoroutine(FadeRoutine());
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
        yield return new WaitForSeconds(holdTime);

        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float alphaMul = 1f - Mathf.Clamp01(t / fadeDuration);

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == null)
                    continue;

                Color c = originalColors[i];
                c.a = originalColors[i].a * alphaMul;
                spriteRenderers[i].color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}