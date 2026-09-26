using UnityEngine;

public static class RuntimeMagicSpriteLibrary
{
    private static Sprite softCircleSprite;
    private static Sprite softRaySprite;
    private static Sprite softRingSprite;
    private static Sprite downArrowSprite;

    public static Sprite SoftCircle
    {
        get
        {
            if (softCircleSprite == null)
                softCircleSprite = CreateSoftCircleSprite();

            return softCircleSprite;
        }
    }

    public static Sprite SoftRay
    {
        get
        {
            if (softRaySprite == null)
                softRaySprite = CreateSoftRaySprite();

            return softRaySprite;
        }
    }

    public static Sprite SoftRing
    {
        get
        {
            if (softRingSprite == null)
                softRingSprite = CreateSoftRingSprite();

            return softRingSprite;
        }
    }

    public static Sprite DownArrow
    {
        get
        {
            if (downArrowSprite == null)
                downArrowSprite = CreateDownArrowSprite();

            return downArrowSprite;
        }
    }

    private static Sprite CreateSoftCircleSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "RuntimeSoftMagicCircle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        texture.hideFlags = HideFlags.HideAndDontSave;

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "RuntimeSoftMagicCircleSprite";
        return sprite;
    }

    private static Sprite CreateSoftRaySprite()
    {
        const int width = 128;
        const int height = 24;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "RuntimeSoftMagicRay";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < height; y++)
        {
            float vertical = Mathf.Abs((y + 0.5f) / height - 0.5f) * 2f;
            float verticalAlpha = Mathf.Clamp01(1f - vertical);

            for (int x = 0; x < width; x++)
            {
                float horizontal = (x + 0.5f) / width;
                float tipFade = Mathf.Sin(horizontal * Mathf.PI);
                float alpha = verticalAlpha * verticalAlpha * tipFade;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        texture.hideFlags = HideFlags.HideAndDontSave;

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "RuntimeSoftMagicRaySprite";
        return sprite;
    }

    private static Sprite CreateSoftRingSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "RuntimeSoftMagicRing";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.42f;
        float thickness = size * 0.08f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - radius) / thickness);
                float alpha = ring * ring;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        texture.hideFlags = HideFlags.HideAndDontSave;

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "RuntimeSoftMagicRingSprite";
        return sprite;
    }

    private static Sprite CreateDownArrowSprite()
    {
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "RuntimeDownArrow";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;

            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float centeredX = Mathf.Abs(u - 0.5f);
                bool inStem = v >= 0.45f && v <= 0.92f && centeredX <= 0.11f;
                float headProgress = Mathf.InverseLerp(0.08f, 0.52f, v);
                float headHalfWidth = Mathf.Lerp(0.03f, 0.38f, headProgress);
                bool inHead = v >= 0.08f && v <= 0.52f && centeredX <= headHalfWidth;
                float alpha = inStem || inHead ? 1f : 0f;

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        texture.hideFlags = HideFlags.HideAndDontSave;

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "RuntimeDownArrowSprite";
        return sprite;
    }
}
