using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CharacterSelectionUiPreviewAndRadarIconBuilder
{
    private const string DefinitionFolder = "Assets/Generated/CharacterSelection/Definitions";
    private const string UiPreviewFolder = "Assets/Generated/CharacterSelection/Prefabs/UICharacters";
    private const string RadarIconFolder = "Assets/Generated/CharacterSelection/Textures/RadarIcons";
    private const string CharacterSelectScenePath = "Assets/Scenes/CharacterSelectScene.unity";

    private static readonly string[] RadarAxisNames =
    {
        "Vitality",
        "Attack",
        "Defense",
        "Mobility",
        "Range",
        "Prediction"
    };

    private static readonly Vector2[] RadarIconPositions =
    {
        new Vector2(0f, 78f),
        new Vector2(82f, 39f),
        new Vector2(82f, -39f),
        new Vector2(0f, -78f),
        new Vector2(-82f, -39f),
        new Vector2(-82f, 39f)
    };

    [MenuItem("Tools/Character Selection/Build UI Preview Prefabs And Radar Icons")]
    public static void BuildAll()
    {
        EnsureRadarIconAssets();
        GenerateUiPreviewPrefabs();
        PatchCharacterSelectScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Character selection UI preview prefabs and radar icons updated.");
    }

    public static void BuildFromCommandLine()
    {
        BuildAll();
    }

    public static void EnsureRadarIconAssets()
    {
        EnsureFolder(RadarIconFolder);

        for (int i = 0; i < RadarAxisNames.Length; i++)
        {
            string path = GetRadarIconPath(i);
            Texture2D texture = CreateRadarIconTexture(i);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureSpriteImporter(path);
        }
    }

    public static Sprite[] LoadRadarIconSprites()
    {
        EnsureRadarIconAssets();

        Sprite[] sprites = new Sprite[RadarAxisNames.Length];
        for (int i = 0; i < sprites.Length; i++)
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(GetRadarIconPath(i));

        return sprites;
    }

    public static void GenerateUiPreviewPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterSelectionDefinition", new[] { DefinitionFolder });
        CharacterSelectionDefinition[] definitions = new CharacterSelectionDefinition[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            definitions[i] = AssetDatabase.LoadAssetAtPath<CharacterSelectionDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));

        GenerateUiPreviewPrefabs(definitions);
    }

    public static void GenerateUiPreviewPrefabs(CharacterSelectionDefinition[] definitions)
    {
        EnsureFolder(UiPreviewFolder);

        if (definitions == null)
            return;

        for (int i = 0; i < definitions.Length; i++)
        {
            CharacterSelectionDefinition definition = definitions[i];
            if (definition == null || string.IsNullOrEmpty(definition.characterId))
                continue;

            int skinCount = Mathf.Max(1, definition.GetSkinCount());
            GameObject[] uiPrefabs = new GameObject[skinCount];
            for (int skinIndex = 0; skinIndex < skinCount; skinIndex++)
            {
                Sprite sprite = definition.GetSkinPortrait(skinIndex);
                if (sprite == null)
                    sprite = definition.portraitSprite != null ? definition.portraitSprite : definition.cardSprite;

                string skinName = definition.GetSkinName(skinIndex);
                uiPrefabs[skinIndex] = CreateUiPreviewPrefab(definition.characterId, skinName, sprite);
            }

            definition.previewPrefab = uiPrefabs.Length > 0 ? uiPrefabs[0] : null;
            definition.skinPreviewPrefabs = uiPrefabs;
            EditorUtility.SetDirty(definition);
        }
    }

    public static Image[] PatchRadarIcons(CharacterRadarChartController controller)
    {
        if (controller == null)
            return new Image[0];

        Image background = PatchRadarBackground(controller);
        Sprite[] sprites = LoadRadarIconSprites();
        Image[] icons = new Image[RadarAxisNames.Length];
        Transform parent = controller.transform;

        for (int i = 0; i < RadarAxisNames.Length; i++)
        {
            string objectName = RadarAxisNames[i] + "Icon";
            Transform existing = parent.Find(objectName);
            GameObject iconObject = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(parent, false);

            RectTransform rect = iconObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = RadarIconPositions[i];
            rect.sizeDelta = new Vector2(20f, 20f);
            rect.localScale = Vector3.one;

            Image image = iconObject.GetComponent<Image>();
            image.sprite = i < sprites.Length ? sprites[i] : null;
            image.color = Color.white;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.enabled = image.sprite != null;
            icons[i] = image;
        }

        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty backgroundProperty = serializedController.FindProperty("backgroundImage");
        if (backgroundProperty != null)
            backgroundProperty.objectReferenceValue = background;

        SerializedProperty iconsProperty = serializedController.FindProperty("axisIcons");
        if (iconsProperty != null && iconsProperty.isArray)
        {
            iconsProperty.arraySize = icons.Length;
            for (int i = 0; i < icons.Length; i++)
                iconsProperty.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
        }

        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        return icons;
    }

    private static Image PatchRadarBackground(CharacterRadarChartController controller)
    {
        Transform chartTransform = controller.transform;
        Transform parent = chartTransform.parent != null ? chartTransform.parent : chartTransform;
        Transform existing = parent.Find("RadarBackgroundImage");
        bool created = existing == null;
        GameObject backgroundObject = existing != null
            ? existing.gameObject
            : new GameObject("RadarBackgroundImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        backgroundObject.transform.SetParent(parent, false);
        RectTransform chartRect = chartTransform as RectTransform;
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        if (chartRect != null && backgroundRect != null)
        {
            backgroundRect.anchorMin = chartRect.anchorMin;
            backgroundRect.anchorMax = chartRect.anchorMax;
            backgroundRect.pivot = chartRect.pivot;
            backgroundRect.anchoredPosition = chartRect.anchoredPosition;
            backgroundRect.sizeDelta = chartRect.sizeDelta;
            backgroundRect.localScale = Vector3.one;
            int chartIndex = chartRect.GetSiblingIndex();
            int backgroundIndex = backgroundRect.GetSiblingIndex();
            int targetIndex = backgroundIndex < chartIndex ? Mathf.Max(0, chartIndex - 1) : chartIndex;
            backgroundRect.SetSiblingIndex(targetIndex);
        }

        Image image = backgroundObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        if (created)
        {
            image.enabled = false;
            backgroundObject.SetActive(false);
        }

        EditorUtility.SetDirty(backgroundObject);
        return image;
    }

    private static void PatchCharacterSelectScene()
    {
        if (!File.Exists(CharacterSelectScenePath))
            return;

        Scene scene = EditorSceneManager.OpenScene(CharacterSelectScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        CharacterRadarChartController[] controllers = GetSceneComponents<CharacterRadarChartController>(scene);
        for (int i = 0; i < controllers.Length; i++)
            PatchRadarIcons(controllers[i]);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject CreateUiPreviewPrefab(string characterId, string skinName, Sprite sprite)
    {
        string safeCharacter = SanitizeFileName(characterId);
        string safeSkin = SanitizeFileName(skinName);
        string path = UiPreviewFolder + "/" + safeCharacter + "_" + safeSkin + "_UIPreview.prefab";

        GameObject root = new GameObject(safeCharacter + "_" + safeSkin + "_UIPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(180f, 180f);

        Image image = root.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = true;

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static Texture2D CreateRadarIconTexture(int index)
    {
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color gold = new Color(0.95f, 0.72f, 0.24f, 1f);
        Color cyan = new Color(0.34f, 0.86f, 1f, 1f);
        Color purple = new Color(0.67f, 0.42f, 1f, 1f);
        Color blue = new Color(0.18f, 0.34f, 0.85f, 0.92f);
        Color red = new Color(0.95f, 0.26f, 0.34f, 1f);
        Color dark = new Color(0.02f, 0.025f, 0.04f, 0.95f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        switch (index)
        {
            case 0:
                DrawHeart(texture, red, gold);
                break;
            case 1:
                DrawSword(texture, cyan, gold, dark);
                break;
            case 2:
                DrawShield(texture, blue, gold);
                break;
            case 3:
                DrawMobility(texture, cyan, gold);
                break;
            case 4:
                DrawCrosshair(texture, purple, gold);
                break;
            default:
                DrawEye(texture, purple, gold, cyan);
                break;
        }

        texture.Apply();
        return texture;
    }

    private static void DrawHeart(Texture2D texture, Color fill, Color outline)
    {
        for (int y = 7; y < 57; y++)
        {
            for (int x = 7; x < 57; x++)
            {
                float nx = (x - 32f) / 17f;
                float ny = (y - 29f) / 17f;
                float v = Mathf.Pow(nx * nx + ny * ny - 1f, 3f) - nx * nx * ny * ny * ny;
                if (v <= 0f)
                    BlendPixel(texture, x, y, fill);
            }
        }

        DrawCircle(texture, new Vector2(24f, 37f), 12f, 3f, outline);
        DrawCircle(texture, new Vector2(40f, 37f), 12f, 3f, outline);
        DrawLine(texture, new Vector2(13f, 31f), new Vector2(32f, 11f), 3f, outline);
        DrawLine(texture, new Vector2(51f, 31f), new Vector2(32f, 11f), 3f, outline);
    }

    private static void DrawSword(Texture2D texture, Color blade, Color accent, Color shadow)
    {
        DrawLine(texture, new Vector2(19f, 45f), new Vector2(47f, 17f), 8f, shadow);
        DrawLine(texture, new Vector2(20f, 44f), new Vector2(46f, 18f), 5f, blade);
        FillPolygon(texture, new[] { new Vector2(44f, 14f), new Vector2(51f, 11f), new Vector2(48f, 18f) }, accent);
        DrawLine(texture, new Vector2(18f, 34f), new Vector2(30f, 46f), 4f, accent);
        DrawLine(texture, new Vector2(14f, 50f), new Vector2(22f, 42f), 5f, accent);
    }

    private static void DrawShield(Texture2D texture, Color fill, Color outline)
    {
        Vector2[] points =
        {
            new Vector2(32f, 9f),
            new Vector2(51f, 17f),
            new Vector2(47f, 40f),
            new Vector2(32f, 55f),
            new Vector2(17f, 40f),
            new Vector2(13f, 17f)
        };

        FillPolygon(texture, points, fill);
        DrawPolygon(texture, points, 4f, outline);
        DrawLine(texture, new Vector2(32f, 14f), new Vector2(32f, 49f), 2f, new Color(1f, 1f, 1f, 0.35f));
    }

    private static void DrawMobility(Texture2D texture, Color fill, Color outline)
    {
        FillPolygon(texture, new[] { new Vector2(17f, 42f), new Vector2(37f, 42f), new Vector2(49f, 30f), new Vector2(29f, 30f) }, fill);
        DrawPolygon(texture, new[] { new Vector2(17f, 42f), new Vector2(37f, 42f), new Vector2(49f, 30f), new Vector2(29f, 30f) }, 3f, outline);
        DrawLine(texture, new Vector2(16f, 48f), new Vector2(48f, 48f), 3f, outline);
        DrawLine(texture, new Vector2(22f, 26f), new Vector2(33f, 15f), 4f, fill);
        DrawLine(texture, new Vector2(33f, 15f), new Vector2(44f, 26f), 4f, fill);
        DrawLine(texture, new Vector2(25f, 24f), new Vector2(33f, 16f), 2f, outline);
        DrawLine(texture, new Vector2(41f, 24f), new Vector2(33f, 16f), 2f, outline);
    }

    private static void DrawCrosshair(Texture2D texture, Color line, Color accent)
    {
        DrawCircle(texture, new Vector2(32f, 32f), 19f, 3f, line);
        DrawCircle(texture, new Vector2(32f, 32f), 7f, 3f, accent);
        DrawLine(texture, new Vector2(32f, 8f), new Vector2(32f, 22f), 3f, line);
        DrawLine(texture, new Vector2(32f, 42f), new Vector2(32f, 56f), 3f, line);
        DrawLine(texture, new Vector2(8f, 32f), new Vector2(22f, 32f), 3f, line);
        DrawLine(texture, new Vector2(42f, 32f), new Vector2(56f, 32f), 3f, line);
    }

    private static void DrawEye(Texture2D texture, Color fill, Color outline, Color iris)
    {
        Vector2[] top =
        {
            new Vector2(10f, 32f),
            new Vector2(20f, 21f),
            new Vector2(32f, 18f),
            new Vector2(44f, 21f),
            new Vector2(54f, 32f)
        };
        Vector2[] bottom =
        {
            new Vector2(10f, 32f),
            new Vector2(20f, 43f),
            new Vector2(32f, 46f),
            new Vector2(44f, 43f),
            new Vector2(54f, 32f)
        };

        DrawPolyline(texture, top, 4f, outline);
        DrawPolyline(texture, bottom, 4f, outline);
        FillCircle(texture, new Vector2(32f, 32f), 10f, fill);
        FillCircle(texture, new Vector2(32f, 32f), 6f, iris);
        FillCircle(texture, new Vector2(32f, 32f), 3f, new Color(0.02f, 0.02f, 0.04f, 1f));
    }

    private static void DrawCircle(Texture2D texture, Vector2 center, float radius, float thickness, Color color)
    {
        int minX = Mathf.FloorToInt(center.x - radius - thickness);
        int maxX = Mathf.CeilToInt(center.x + radius + thickness);
        int minY = Mathf.FloorToInt(center.y - radius - thickness);
        int maxY = Mathf.CeilToInt(center.y + radius + thickness);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (Mathf.Abs(distance - radius) <= thickness * 0.5f)
                    BlendPixel(texture, x, y, color);
            }
        }
    }

    private static void FillCircle(Texture2D texture, Vector2 center, float radius, Color color)
    {
        int minX = Mathf.FloorToInt(center.x - radius);
        int maxX = Mathf.CeilToInt(center.x + radius);
        int minY = Mathf.FloorToInt(center.y - radius);
        int maxY = Mathf.CeilToInt(center.y + radius);
        float sqrRadius = radius * radius;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                if (delta.sqrMagnitude <= sqrRadius)
                    BlendPixel(texture, x, y, color);
            }
        }
    }

    private static void DrawPolyline(Texture2D texture, Vector2[] points, float thickness, Color color)
    {
        for (int i = 0; i < points.Length - 1; i++)
            DrawLine(texture, points[i], points[i + 1], thickness, color);
    }

    private static void DrawPolygon(Texture2D texture, Vector2[] points, float thickness, Color color)
    {
        for (int i = 0; i < points.Length; i++)
            DrawLine(texture, points[i], points[(i + 1) % points.Length], thickness, color);
    }

    private static void DrawLine(Texture2D texture, Vector2 start, Vector2 end, float thickness, Color color)
    {
        int minX = Mathf.FloorToInt(Mathf.Min(start.x, end.x) - thickness);
        int maxX = Mathf.CeilToInt(Mathf.Max(start.x, end.x) + thickness);
        int minY = Mathf.FloorToInt(Mathf.Min(start.y, end.y) - thickness);
        int maxY = Mathf.CeilToInt(Mathf.Max(start.y, end.y) + thickness);
        Vector2 line = end - start;
        float lineSqr = Mathf.Max(0.0001f, line.sqrMagnitude);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = new Vector2(x, y);
                float t = Mathf.Clamp01(Vector2.Dot(point - start, line) / lineSqr);
                Vector2 closest = start + line * t;
                if (Vector2.Distance(point, closest) <= thickness * 0.5f)
                    BlendPixel(texture, x, y, color);
            }
        }
    }

    private static void FillPolygon(Texture2D texture, Vector2[] points, Color color)
    {
        Rect bounds = GetBounds(points);
        for (int y = Mathf.FloorToInt(bounds.yMin); y <= Mathf.CeilToInt(bounds.yMax); y++)
        {
            for (int x = Mathf.FloorToInt(bounds.xMin); x <= Mathf.CeilToInt(bounds.xMax); x++)
            {
                if (PointInPolygon(new Vector2(x, y), points))
                    BlendPixel(texture, x, y, color);
            }
        }
    }

    private static Rect GetBounds(Vector2[] points)
    {
        float minX = points[0].x;
        float maxX = points[0].x;
        float minY = points[0].y;
        float maxY = points[0].y;
        for (int i = 1; i < points.Length; i++)
        {
            minX = Mathf.Min(minX, points[i].x);
            maxX = Mathf.Max(maxX, points[i].x);
            minY = Mathf.Min(minY, points[i].y);
            maxY = Mathf.Max(maxY, points[i].y);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            bool intersect = ((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                             (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / Mathf.Max(0.0001f, polygon[j].y - polygon[i].y) + polygon[i].x);
            if (intersect)
                inside = !inside;
        }

        return inside;
    }

    private static void BlendPixel(Texture2D texture, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            return;

        Color current = texture.GetPixel(x, y);
        float alpha = color.a + current.a * (1f - color.a);
        if (alpha <= 0f)
            return;

        Color blended = (color * color.a + current * current.a * (1f - color.a)) / alpha;
        blended.a = alpha;
        texture.SetPixel(x, y, blended);
    }

    private static void ConfigureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 128;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }

    private static T[] GetSceneComponents<T>(Scene scene) where T : Component
    {
        List<T> components = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            components.AddRange(roots[i].GetComponentsInChildren<T>(true));

        return components.ToArray();
    }

    private static string GetRadarIconPath(int index)
    {
        return RadarIconFolder + "/" + RadarAxisNames[index] + "_RadarIcon.png";
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "Default";

        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            for (int j = 0; j < invalid.Length; j++)
            {
                if (chars[i] == invalid[j])
                {
                    chars[i] = '_';
                    break;
                }
            }

            if (char.IsWhiteSpace(chars[i]))
                chars[i] = '_';
        }

        return new string(chars);
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
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
