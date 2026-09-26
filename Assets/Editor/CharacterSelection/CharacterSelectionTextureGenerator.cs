using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CharacterSelectionTextureGenerator
{
    public const string TextureFolder = "Assets/Generated/CharacterSelection/Textures";

    public static Dictionary<string, Sprite> GenerateTextures()
    {
        EnsureFolder(TextureFolder);

        WriteTexture("SoftGlow", DrawSoftGlow(128), TextureWrapMode.Clamp);
        WriteTexture("LightningStrip", DrawLightningStrip(256, 64), TextureWrapMode.Clamp);
        WriteTexture("PurpleFogNoise", DrawFogNoise(128), TextureWrapMode.Repeat);
        WriteTexture("RadarPoint", DrawRadarPoint(32), TextureWrapMode.Clamp);
        WriteTexture("WhiteFlash", DrawWhiteFlash(32), TextureWrapMode.Clamp);
        WriteTexture("Vignette", DrawVignette(256), TextureWrapMode.Clamp);

        AssetDatabase.Refresh();

        string[] names = { "SoftGlow", "LightningStrip", "PurpleFogNoise", "RadarPoint", "WhiteFlash", "Vignette" };
        Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        for (int i = 0; i < names.Length; i++)
        {
            string path = GetPath(names[i]);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = names[i] == "PurpleFogNoise" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            sprites[names[i]] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        AssetDatabase.SaveAssets();
        return sprites;
    }

    private static Texture2D DrawSoftGlow(int size)
    {
        Texture2D texture = NewTexture(size, size);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha;
                texture.SetPixel(x, y, new Color(1f, 0.9f, 0.55f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D DrawLightningStrip(int width, int height)
    {
        Texture2D texture = NewTexture(width, height);
        int y = height / 2;
        for (int x = 8; x < width - 8; x++)
        {
            y += Random.Range(-3, 4);
            y = Mathf.Clamp(y, 10, height - 10);
            DrawDisc(texture, x, y, 2, new Color(1f, 1f, 1f, 1f));
            DrawDisc(texture, x, y, 5, new Color(0.45f, 0.85f, 1f, 0.22f));
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D DrawFogNoise(int size)
    {
        Texture2D texture = NewTexture(size, size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.PerlinNoise(x / 18f, y / 18f);
                float ny = Mathf.PerlinNoise((x + size) / 18f, y / 18f);
                float value = (nx + ny) * 0.5f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, value * 0.55f));
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D DrawRadarPoint(int size)
    {
        Texture2D texture = NewTexture(size, size);
        DrawDisc(texture, size / 2, size / 2, size / 4, Color.white);
        DrawDisc(texture, size / 2, size / 2, size / 2 - 2, new Color(1f, 1f, 1f, 0.2f));
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawWhiteFlash(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, Color.white);
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D DrawVignette(int size)
    {
        Texture2D texture = NewTexture(size, size);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.SmoothStep(0f, 0.72f, Mathf.Clamp01(distance - 0.25f));
                texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D NewTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
        }

        return texture;
    }

    private static void DrawDisc(Texture2D texture, int cx, int cy, int radius, Color color)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int px = cx + x;
                int py = cy + y;
                if (px < 0 || py < 0 || px >= texture.width || py >= texture.height)
                    continue;

                if (x * x + y * y <= radius * radius)
                    texture.SetPixel(px, py, color);
            }
        }
    }

    private static void WriteTexture(string name, Texture2D texture, TextureWrapMode wrapMode)
    {
        string path = GetPath(name);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
    }

    public static string GetPath(string name)
    {
        return TextureFolder + "/" + name + ".png";
    }

    public static void EnsureFolder(string folder)
    {
        string[] parts = folder.Replace("\\", "/").Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
