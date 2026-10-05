using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FullscreenSettingsSetup
{
    [MenuItem("Tools/UI/Install Fullscreen Settings Buttons")]
    public static void Install()
    {
        const string imagePath = "Assets/Scenes/UI/FullScreen.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
        if (sprite == null) throw new Exception("Fullscreen sprite was not imported.");
        foreach (string name in new[] { "StartScene", "LobbyScene" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            var setting = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                .Single(x => x.name == "Setting");
            var language = setting.Find("Mask/LanguageDropdown") as RectTransform;
            if (language == null) throw new Exception(name + " has no language dropdown.");
            var existing = language.Find("FullScreen");
            var obj = existing != null ? existing.gameObject : new GameObject("FullScreen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.layer = language.gameObject.layer;
            obj.transform.SetParent(language, false);
            obj.transform.SetAsFirstSibling();
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(90, 90);
            rect.anchoredPosition = new Vector2(-65, 0);
            var image = obj.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            obj.GetComponent<Button>().targetGraphic = image;
            if (obj.GetComponent<FullscreenButton>() == null) obj.AddComponent<FullscreenButton>();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("FULLSCREEN_SETUP_OK: " + name);
        }
        AssetDatabase.SaveAssets();
    }
}
