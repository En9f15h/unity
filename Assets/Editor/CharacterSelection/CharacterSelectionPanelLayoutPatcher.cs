using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CharacterSelectionPanelLayoutPatcher
{
    private const string CharacterSelectScenePath = "Assets/Scenes/CharacterSelectScene.unity";

    [MenuItem("Tools/Character Selection/Patch Player Panel Button Layout")]
    public static void PatchScene()
    {
        Scene scene = EditorSceneManager.OpenScene(CharacterSelectScenePath, OpenSceneMode.Single);
        Transform youPanel = FindSceneTransform("YouPanel");
        if (youPanel == null)
        {
            Debug.LogError("CharacterSelectionPanelLayoutPatcher: YouPanel was not found.");
            return;
        }

        PatchYouPanel(youPanel);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Character selection player panel button layout patched.");
    }

    public static void PatchFromCommandLine()
    {
        PatchScene();
        EditorApplication.Exit(0);
    }

    private static void PatchYouPanel(Transform youPanel)
    {
        TMP_Text characterName = FindChildComponent<TMP_Text>(youPanel, "CharacterNameText");
        if (characterName != null)
            characterName.rectTransform.sizeDelta = new Vector2(220f, 32f);

        ApplyRect(FindChild(youPanel, "PreviousCharacterButton"), new Vector2(0.5f, 1f), new Vector2(-156f, -428f), new Vector2(42f, 36f));
        ApplyRect(FindChild(youPanel, "NextCharacterButton"), new Vector2(0.5f, 1f), new Vector2(156f, -428f), new Vector2(42f, 36f));

        TMP_Text skinHeader = EnsureText(youPanel, "SkinHeaderText", characterName);
        skinHeader.text = "SKIN";
        skinHeader.fontSize = 18f;
        skinHeader.alignment = TextAlignmentOptions.Center;
        skinHeader.color = new Color(0.88f, 0.92f, 1f, 1f);
        skinHeader.raycastTarget = false;
        ApplyRect(skinHeader.transform, new Vector2(0.5f, 0f), new Vector2(0f, 318f), new Vector2(96f, 28f));

        ApplyRect(FindChild(youPanel, "PreviousSkinButton"), new Vector2(0.5f, 0f), new Vector2(-112f, 318f), new Vector2(44f, 36f));
        ApplyRect(FindChild(youPanel, "NextSkinButton"), new Vector2(0.5f, 0f), new Vector2(112f, 318f), new Vector2(44f, 36f));
    }

    private static TMP_Text EnsureText(Transform parent, string childName, TMP_Text template)
    {
        TMP_Text text = FindChildComponent<TMP_Text>(parent, childName);
        if (text != null)
            return text;

        GameObject textObject = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        text = textObject.GetComponent<TextMeshProUGUI>();
        if (template != null)
        {
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
        }

        return text;
    }

    private static void ApplyRect(Transform target, Vector2 anchor, Vector2 position, Vector2 size)
    {
        if (target == null)
            return;

        RectTransform rect = target as RectTransform;
        if (rect == null)
            rect = target.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static Transform FindSceneTransform(string objectName)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChild(roots[i].transform, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static T FindChildComponent<T>(Transform root, string childName) where T : Component
    {
        Transform child = FindChild(root, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
