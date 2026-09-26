using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class SelectSceneUiImageBinder
{
    private const string CharacterSelectScenePath = "Assets/Scenes/CharacterSelectScene.unity";
    private const string UiFolder = "Assets/Scenes/UI/SelectScene";
    private const string KnightDefinitionPath = "Assets/Generated/CharacterSelection/Definitions/KnightSelectionDefinition.asset";
    private const string OracleDefinitionPath = "Assets/Generated/CharacterSelection/Definitions/OracleSelectionDefinition.asset";

    [MenuItem("Tools/Character Selection/Bind Select Scene UI Images")]
    public static void BindMenu()
    {
        BindSceneAndSave();
    }

    public static void BindFromCommandLine()
    {
        BindSceneAndSave();
        EditorApplication.Exit(0);
    }

    public static void BindSceneAndSave()
    {
        Scene scene = EditorSceneManager.OpenScene(CharacterSelectScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("SelectSceneUiImageBinder: could not open " + CharacterSelectScenePath);
            return;
        }

        ApplyToActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SelectSceneUiImageBinder: SelectScene UI images bound.");
    }

    public static void ApplyToActiveScene()
    {
        if (!Directory.Exists(ToSystemPath(UiFolder)))
        {
            Debug.LogWarning("SelectSceneUiImageBinder: missing UI folder: " + UiFolder);
            return;
        }

        Sprite mainBackground = LoadLargestSprite("09_31_35 (1)");
        Sprite sidePanel = LoadLargestSprite("09_31_36 (2)");
        Sprite topBar = LoadLargestSprite("09_31_36 (3)");
        Sprite previewFrame = LoadLargestSprite("09_31_37 (4)", sprite => sprite.rect.x < 700f && sprite.rect.width > 100f);
        Sprite hiddenPreviewFrame = LoadLargestSprite("09_31_37 (4)", sprite => sprite.rect.x > 700f && sprite.rect.width > 100f);
        Sprite vsFrame = LoadLargestSprite("09_31_37 (5)");
        Sprite skillPopup = LoadLargestSprite("09_31_38 (7)");
        Sprite cardSmall = LoadLargestSprite("09_31_39 (8)", sprite => sprite.rect.width < 400f);
        Sprite cardLarge = LoadLargestSprite("09_31_39 (8)", sprite => sprite.rect.width >= 400f);
        Sprite mapSelector = LoadLargestSprite("09_31_39 (9)");
        Sprite arrowLeft = LoadLargestSprite("09_31_40 (10)", sprite => sprite.rect.width < 250f && sprite.rect.x < 250f);
        Sprite arrowRight = LoadLargestSprite("09_31_40 (10)", sprite => sprite.rect.width < 250f && sprite.rect.x > 300f);
        Sprite buttonWide = LoadLargestSprite("09_31_40 (10)", sprite => sprite.rect.width >= 350f && sprite.rect.width < 500f && sprite.rect.height <= 120f);
        Sprite infoPanel = LoadLargestSprite("09_31_40 (10)", sprite => sprite.rect.width >= 700f);
        Sprite radar = LoadLargestSprite("c376e36c");
        Sprite privacyEye = LoadOrderedSprite("09_12_35", 0);
        Sprite privacyEyeChecked = LoadOrderedSprite("09_12_35", 1);

        BindSceneBackground(mainBackground);
        SetImage("CharacterSelectCanvas/TopBar", topBar, false);
        SetImage("CharacterSelectCanvas/YouPanel", sidePanel, false);
        SetImage("CharacterSelectCanvas/EnemyPanel", sidePanel, false);

        BindPlayerPanel("CharacterSelectCanvas/YouPanel", previewFrame, infoPanel, radar, arrowLeft, arrowRight, buttonWide);
        BindPlayerPanel("CharacterSelectCanvas/EnemyPanel", hiddenPreviewFrame != null ? hiddenPreviewFrame : previewFrame, infoPanel, radar, null, null, null);

        SetImage("CharacterSelectCanvas/CenterPanel", null, false, new Color(1f, 1f, 1f, 0f));
        SetImage("CharacterSelectCanvas/CenterPanel/LightningImage", vsFrame, true);
        ResizeRect("CharacterSelectCanvas/CenterPanel/LightningImage", new Vector2(430f, 120f));
        SetText("CharacterSelectCanvas/CenterPanel/VSText", string.Empty);

        SetImage("CharacterSelectCanvas/MapSelector", mapSelector, false);
        SetImage("CharacterSelectCanvas/MapSelector/PreviousMapButton", arrowLeft, true);
        SetImage("CharacterSelectCanvas/MapSelector/NextMapButton", arrowRight, true);
        ClearButtonLabel("CharacterSelectCanvas/MapSelector/PreviousMapButton");
        ClearButtonLabel("CharacterSelectCanvas/MapSelector/NextMapButton");

        SetImage("CharacterSelectCanvas/SkillPopup", skillPopup, false);
        SetImage("CharacterSelectCanvas/EnemySkillPopup", skillPopup, false);
        SetImage("CharacterSelectCanvas/SkillPopup/ButtonRoot", cardSmall, false);
        SetImage("CharacterSelectCanvas/EnemySkillPopup/ButtonRoot", cardSmall, false);

        SetImage("CharacterSelectCanvas/TopBar/BackButton", arrowLeft, true);
        ClearButtonLabel("CharacterSelectCanvas/TopBar/BackButton");
        BindPrivacyToggle(privacyEye, privacyEyeChecked);
        UpdateSelectionDefinitions(sidePanel, previewFrame, infoPanel, radar);
        PatchPanelSerializedDefaults(sidePanel, previewFrame, infoPanel);
        PatchHiddenPreviewSprite(hiddenPreviewFrame);
        ApplyLayoutControllers();
    }

    private static void BindSceneBackground(Sprite mainBackground)
    {
        SetImage("CharacterSelectCanvas/Background", mainBackground, false);
        SetActive("CharacterSelectCanvas/Background/BlueNightBand", false);
        SetActive("CharacterSelectCanvas/Background/PurpleFogLeft", false);
        SetActive("CharacterSelectCanvas/Background/PurpleFogRight", false);
        SetActive("CharacterSelectCanvas/Background/Vignette", false);
    }

    private static void BindPlayerPanel(
        string panelPath,
        Sprite previewFrame,
        Sprite infoPanel,
        Sprite radar,
        Sprite arrowLeft,
        Sprite arrowRight,
        Sprite buttonWide)
    {
        SetImage(panelPath + "/CharacterPreviewRoot", previewFrame, false);
        SetImage(panelPath + "/CharacterInfo", infoPanel, false);
        SetImage(panelPath + "/RadarBackgroundImage", radar, true);
        SetActive(panelPath + "/RadarBackgroundImage", true);

        if (arrowLeft != null)
        {
            SetImage(panelPath + "/PreviousCharacterButton", arrowLeft, true);
            SetImage(panelPath + "/PreviousSkinButton", arrowLeft, true);
            ClearButtonLabel(panelPath + "/PreviousCharacterButton");
            ClearButtonLabel(panelPath + "/PreviousSkinButton");
        }

        if (arrowRight != null)
        {
            SetImage(panelPath + "/NextCharacterButton", arrowRight, true);
            SetImage(panelPath + "/NextSkinButton", arrowRight, true);
            ClearButtonLabel(panelPath + "/NextCharacterButton");
            ClearButtonLabel(panelPath + "/NextSkinButton");
        }

        if (buttonWide != null)
        {
            SetImage(panelPath + "/ConfirmButton", buttonWide, false);
            SetImage(panelPath + "/CancelReadyButton", buttonWide, false);
        }
    }

    private static void BindPrivacyToggle(Sprite privacyEye, Sprite privacyEyeChecked)
    {
        SetImage("CharacterSelectCanvas/TopBar/PublicSelectionToggle", null, true, new Color(1f, 1f, 1f, 0f));
        SetImage("CharacterSelectCanvas/TopBar/PublicSelectionToggle/Background", privacyEye, true);
        SetImage("CharacterSelectCanvas/TopBar/PublicSelectionToggle/Background/Checkmark", privacyEyeChecked, true);
        ClearTextRecursive("CharacterSelectCanvas/TopBar/PublicSelectionToggle/Label");
        PatchPrivacyToggleSprites(privacyEye, privacyEyeChecked);
    }

    private static void PatchPrivacyToggleSprites(Sprite defaultSprite, Sprite pressedSprite)
    {
        CharacterSelectionPrivacyController controller = Object.FindObjectsByType<CharacterSelectionPrivacyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault();
        if (controller == null)
            return;

        SerializedObject serialized = new SerializedObject(controller);
        SetSerializedReference(serialized, "defaultToggleSprite", defaultSprite);
        SetSerializedReference(serialized, "pressedToggleSprite", pressedSprite);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        Toggle toggle = controller.PublicSelectionToggle;
        if (toggle != null)
        {
            toggle.transition = Selectable.Transition.None;
            toggle.graphic = null;
            EditorUtility.SetDirty(toggle);
        }
    }

    private static void UpdateSelectionDefinitions(Sprite sidePanel, Sprite previewFrame, Sprite infoPanel, Sprite radar)
    {
        UpdateSelectionDefinition(KnightDefinitionPath, sidePanel, previewFrame, infoPanel, radar);
        UpdateSelectionDefinition(OracleDefinitionPath, sidePanel, previewFrame, infoPanel, radar);
    }

    private static void UpdateSelectionDefinition(string path, Sprite sidePanel, Sprite previewFrame, Sprite infoPanel, Sprite radar)
    {
        CharacterSelectionDefinition definition = AssetDatabase.LoadAssetAtPath<CharacterSelectionDefinition>(path);
        if (definition == null)
            return;

        definition.selectionPanelBackgroundSprite = sidePanel;
        definition.characterPreviewBackgroundSprite = previewFrame;
        definition.characterInfoBackgroundSprite = infoPanel;
        definition.radarBackgroundSprite = radar;
        EditorUtility.SetDirty(definition);
    }

    private static void PatchPanelSerializedDefaults(Sprite sidePanel, Sprite previewFrame, Sprite infoPanel)
    {
        CharacterSelectionPlayerPanel[] panels = Object.FindObjectsByType<CharacterSelectionPlayerPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < panels.Length; i++)
        {
            CharacterSelectionPlayerPanel panel = panels[i];
            if (panel == null)
                continue;

            SerializedObject serialized = new SerializedObject(panel);
            SetSerializedReference(serialized, "panelBackgroundImage", GetImage(panel.transform));
            SetSerializedReference(serialized, "characterPreviewBackgroundImage", GetImage(FindChild(panel.transform, "CharacterPreviewRoot")));
            SetSerializedReference(serialized, "characterInfoBackgroundImage", GetImage(FindChild(panel.transform, "CharacterInfo")));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel);
        }
    }

    private static void PatchHiddenPreviewSprite(Sprite hiddenPreviewFrame)
    {
        if (hiddenPreviewFrame == null)
            return;

        CharacterPreviewController[] previews = Object.FindObjectsByType<CharacterPreviewController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < previews.Length; i++)
        {
            SerializedObject serialized = new SerializedObject(previews[i]);
            SetSerializedReference(serialized, "hiddenPortraitSprite", hiddenPreviewFrame);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(previews[i]);
        }
    }

    private static void SetImage(string path, Sprite sprite, bool preserveAspect)
    {
        SetImage(path, sprite, preserveAspect, Color.white);
    }

    private static void SetImage(string path, Sprite sprite, bool preserveAspect, Color color)
    {
        Transform transform = FindPath(path);
        if (transform == null)
            return;

        Image image = transform.GetComponent<Image>();
        if (image == null)
            image = transform.gameObject.AddComponent<Image>();

        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null && HasSpriteBorder(sprite) ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = image.raycastTarget && transform.GetComponent<Button>() != null;
        image.enabled = color.a > 0f && sprite != null;
        EditorUtility.SetDirty(image);
    }

    private static bool HasSpriteBorder(Sprite sprite)
    {
        if (sprite == null)
            return false;

        Vector4 border = sprite.border;
        return border.x > 0f || border.y > 0f || border.z > 0f || border.w > 0f;
    }

    private static void ApplyLayoutControllers()
    {
        CharacterSelectionUILayoutController[] layouts = Object.FindObjectsByType<CharacterSelectionUILayoutController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < layouts.Length; i++)
        {
            if (layouts[i] == null)
                continue;

            layouts[i].ApplyLayout();
            EditorUtility.SetDirty(layouts[i]);
        }
    }

    private static void ResizeRect(string path, Vector2 size)
    {
        RectTransform rect = FindPath(path) as RectTransform;
        if (rect == null)
            return;

        rect.sizeDelta = size;
        EditorUtility.SetDirty(rect);
    }

    private static void SetActive(string path, bool active)
    {
        Transform transform = FindPath(path);
        if (transform == null)
            return;

        transform.gameObject.SetActive(active);
        EditorUtility.SetDirty(transform.gameObject);
    }

    private static void SetText(string path, string text)
    {
        Transform transform = FindPath(path);
        if (transform == null)
            return;

        TMP_Text tmp = transform.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = text;
            EditorUtility.SetDirty(tmp);
            return;
        }

        Text legacy = transform.GetComponent<Text>();
        if (legacy != null)
        {
            legacy.text = text;
            EditorUtility.SetDirty(legacy);
        }
    }

    private static void ClearButtonLabel(string path)
    {
        Transform transform = FindPath(path);
        if (transform == null)
            return;

        TMP_Text[] tmpTexts = transform.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            tmpTexts[i].text = string.Empty;
            EditorUtility.SetDirty(tmpTexts[i]);
        }

        Text[] texts = transform.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i].text = string.Empty;
            EditorUtility.SetDirty(texts[i]);
        }
    }

    private static void ClearTextRecursive(string path)
    {
        ClearButtonLabel(path);
    }

    private static Sprite LoadOrderedSprite(string textureToken, int index)
    {
        string path = FindTexturePath(textureToken);
        List<Sprite> sprites = LoadSprites(path)
            .Where(sprite => sprite.rect.width > 10f && sprite.rect.height > 10f)
            .OrderBy(sprite => sprite.rect.x)
            .ToList();

        if (index < 0 || index >= sprites.Count)
            return null;

        return sprites[index];
    }

    private static Sprite LoadLargestSprite(string textureToken, Func<Sprite, bool> predicate = null)
    {
        string path = FindTexturePath(textureToken);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("SelectSceneUiImageBinder: missing texture token " + textureToken);
            return null;
        }

        IEnumerable<Sprite> sprites = LoadSprites(path);
        if (predicate != null)
            sprites = sprites.Where(predicate);

        return sprites
            .Where(sprite => sprite != null)
            .OrderByDescending(sprite => sprite.rect.width * sprite.rect.height)
            .FirstOrDefault();
    }

    private static List<Sprite> LoadSprites(string path)
    {
        List<Sprite> sprites = new List<Sprite>();
        if (string.IsNullOrEmpty(path))
            return sprites;

        Sprite main = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (main != null)
            sprites.Add(main);

        Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && !sprites.Contains(sprite))
                sprites.Add(sprite);
        }

        return sprites;
    }

    private static string FindTexturePath(string token)
    {
        string systemFolder = ToSystemPath(UiFolder);
        if (!Directory.Exists(systemFolder))
            return null;

        string file = Directory.GetFiles(systemFolder, "*.png", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => Path.GetFileName(path).Contains(token, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(file))
            return null;

        return ToUnityPath(file);
    }

    private static Transform FindPath(string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length == 0)
            return null;

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != parts[0])
                continue;

            Transform current = roots[i].transform;
            for (int p = 1; p < parts.Length && current != null; p++)
                current = FindDirectChild(current, parts[p]);

            if (current != null)
                return current;
        }

        return null;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;
        }

        return null;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        if (parent == null)
            return null;

        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChild(parent.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Image GetImage(Transform transform)
    {
        return transform != null ? transform.GetComponent<Image>() : null;
    }

    private static void SetSerializedReference(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static string ToSystemPath(string unityPath)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), unityPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string ToUnityPath(string systemPath)
    {
        return systemPath.Replace('\\', '/').Replace(Directory.GetCurrentDirectory().Replace('\\', '/') + "/", string.Empty);
    }
}
