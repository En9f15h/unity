using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SelectionPolishSetup
{
    public const string Output = "CodexLogs/SelectionPolish-20260916";
    public static void Install()
    {
        Directory.CreateDirectory(Output);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/CharacterSelectScene.unity");
        var layout = UnityEngine.Object.FindFirstObjectByType<CharacterSelectionUILayoutController>();
        var old = new SerializedObject(layout);
        var component = layout.GetComponent<SelectionPresentationLayout>();
        if (component == null) component = layout.gameObject.AddComponent<SelectionPresentationLayout>();
        var data = new SerializedObject(component);
        foreach (string name in new[]{"youPanel","enemyPanel"}) data.FindProperty(name).objectReferenceValue = old.FindProperty(name).objectReferenceValue;
        data.FindProperty("mapPanel").objectReferenceValue = old.FindProperty("mapSelectorPanel").objectReferenceValue;
        var you = (RectTransform)old.FindProperty("youSkillPopup").objectReferenceValue;
        var enemy = (RectTransform)old.FindProperty("enemySkillPopup").objectReferenceValue;
        data.FindProperty("youSkills").objectReferenceValue = you.GetComponent<CharacterSkillPopup>();
        data.FindProperty("enemySkills").objectReferenceValue = enemy.GetComponent<CharacterSkillPopup>();
        var audio = layout.transform.Find("AudioVolumeControlUI") as RectTransform;
        if (audio == null) throw new InvalidOperationException("Missing audio volume panel");
        audio.anchorMin = audio.anchorMax = new Vector2(.5f,1); audio.pivot = new Vector2(.5f,1);
        audio.anchoredPosition = new Vector2(0,-104);
        audio.sizeDelta = new Vector2(602,224);
        int rowIndex = 0;
        foreach (Transform child in audio)
        {
            var row = child as RectTransform;
            if (row == null) continue;
            row.anchoredPosition = new Vector2(0,-48-64*rowIndex++);
            row.sizeDelta = new Vector2(-64,48);
        }
        audio.SetAsLastSibling();
        data.FindProperty("volumePanel").objectReferenceValue = audio.gameObject;
        // Reuse the authored dark metal frame; leave all scrollbar callbacks and audio values intact.
        var player = ((RectTransform)old.FindProperty("youPanel").objectReferenceValue).GetComponent<CharacterSelectionPlayerPanel>();
        var frame = player.ConfirmButton.image.sprite;
        var audioImage = audio.GetComponent<Image>();
        audioImage.sprite = frame; audioImage.type = Image.Type.Sliced; audioImage.color = Color.white;
        foreach (var text in audio.GetComponentsInChildren<Text>(true)) { text.fontSize = 16; text.color = new Color(.86f,.91f,.96f); }
        var top = (RectTransform)old.FindProperty("topBar").objectReferenceValue;
        var existing = top.Find("VolumeToggle");
        var button = existing != null ? existing.GetComponent<Button>() : null;
        if (button == null)
        {
            var root = new GameObject("VolumeToggle",typeof(RectTransform),typeof(Image),typeof(Button)); root.transform.SetParent(top,false);
            var rect = (RectTransform)root.transform; rect.anchorMin = rect.anchorMax = new Vector2(0,.5f);
            rect.anchoredPosition = new Vector2(178,0); rect.sizeDelta = new Vector2(128,52);
            button = root.GetComponent<Button>(); var image = root.GetComponent<Image>();
            var template = top.GetComponentsInChildren<Button>(true).First(b => b != button);
            image.sprite = template.image.sprite; image.type = Image.Type.Sliced; button.targetGraphic = image; button.colors = template.colors;
            var label = new GameObject("Label",typeof(RectTransform)).AddComponent<TextMeshProUGUI>(); label.transform.SetParent(root.transform,false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = Vector2.zero;
            var fontSource = you.GetComponentsInChildren<TMP_Text>(true).First(); label.font = fontSource.font; label.fontSharedMaterial = fontSource.fontSharedMaterial;
            label.text = "音量"; label.fontSize = 18; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        }
        button.image.sprite = frame;
        var volumeText = button.GetComponentInChildren<TMP_Text>();
        volumeText.font = LabelFont();
        volumeText.fontSharedMaterial = volumeText.font.material;
        volumeText.fontSize = 20;
        data.FindProperty("volumeButton").objectReferenceValue = button;
        data.FindProperty("volumeLabel").objectReferenceValue = button.GetComponentInChildren<TMP_Text>();
        data.ApplyModifiedPropertiesWithoutUndo(); component.ApplyLayout(); audio.gameObject.SetActive(false);
        // Changes belong only to this scene's prefab instance, not the shared volume-control prefab.
        PrefabUtility.RecordPrefabInstancePropertyModifications(audio.gameObject);
        foreach (var part in audio.GetComponentsInChildren<Component>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(part);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }

    private static TMP_FontAsset LabelFont()
    {
        const string path = "Assets/Resources/Combat/SelectionLabelFont.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font != null) return font;
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Resources/Fonts & Materials/MSJHL.TTC");
        font = TMP_FontAsset.CreateFontAsset(source,48,6,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,512,512,AtlasPopulationMode.Dynamic,false);
        font.name = "SelectionLabelFont";
        AssetDatabase.CreateAsset(font,path);
        AssetDatabase.AddObjectToAsset(font.atlasTextures[0],font);
        AssetDatabase.AddObjectToAsset(font.material,font);
        if (!font.TryAddCharacters("音量Audio ×",out string missing)) throw new InvalidOperationException("Missing volume-label glyphs: " + missing);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font); return font;
    }
}
