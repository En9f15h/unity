using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.PropertyVariants;
using UnityEngine.Localization.PropertyVariants.TrackedObjects;
using UnityEngine.Localization.PropertyVariants.TrackedProperties;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class CodexJapaneseLocalizationSetup
{
    private const string JapaneseTablePath = "Assets/Localization/StringTable_ja-JP.asset";
    private const string SourceFontPath = "Assets/TextMesh Pro/Fonts/YuGothR.ttc";
    private const string JapaneseFontPath = "Assets/TextMesh Pro/Fonts/Yu Gothic UI SDF.asset";
    private const string JapaneseLocaleCode = "ja-JP";
    private static readonly string[] SceneSearchFolders = { "Assets/Scenes" };
    private static readonly string[] PrefabSearchFolders = { "Assets/Resources", "Assets/Generated" };

    private static readonly LocaleIdentifier JapaneseLocale = new LocaleIdentifier(JapaneseLocaleCode);

    private static readonly (long id, string text)[] JapaneseEntries =
    {
        (196255469568, "終了"),
        (22881660358656, "スタート!"),
        (23305515749376, "パスワード（任意）"),
        (23621502029824, "ルーム名"),
        (23776011800576, "サーバー"),
        (23933419835392, "パスワード"),
        (24057604788224, "名前"),
        (24226882703360, "ルーム一覧"),
        (27620112388096, "ランダム参加"),
        (27769500913664, "作成して参加"),
        (27977068630016, "ルーム作成"),
        (28113870049280, "ルーム参加"),
        (28592888926208, "パスワードなしのルームだけがランダム参加の対象になります。"),
        (29415748456448, "選択を表示"),
        (29935712129024, "スキン"),
        (32341095141376, "相手を挑発しながら、確実に+1エネルギーを得る。"),
        (32550441242624, "低い攻撃と素早い攻撃を回避するが、重いダメージは軽減するだけ。"),
        (32912925577216, "移動は攻撃範囲判定より先に処理されるため、回避にもなれば移動中に命中することもある。"),
        (33679145226240, "敵の選択した行動を明かし、情報を得る。"),
        (33971504992256, "素早い攻撃を防ぎ、一部の重いダメージを軽減する。"),
        (34226413817856, "敵の背後へ瞬間移動し、位置を入れ替える。"),
        (34490659164160, "その場で消え、動かずに高リスク攻撃を完全に回避する。"),
        (34968004513792, "チャージ型の魔法攻撃。特定の防御を貫通する。"),
        (35438840303616, "距離が遠いほど大きなダメージを与える遠距離攻撃。"),
        (35665538240512, "非常に高いダメージを与える強力な射程2攻撃。"),
        (35848435060736, "強固なガードで受ける攻撃を防ぐ。"),
        (36180397445120, "特定の攻撃を相手に跳ね返す。"),
        (36395649126400, "防御行動を崩す低い一撃。"),
        (36657872818176, "高ダメージの強力なチャージ攻撃。"),
        (36844502568960, "チャージ中のスキルを妨害できる素早い近距離攻撃。"),
        (37000000000000, "安定した防御、確実な移動、直接的な圧力を備えたバランス型の近接戦士。"),
        (37000000000001, "間合いを操り、意図を見抜き、敵の計画を罰する予測型の術者。"),
        (37000000000002, "ルーム: --"),
        (37000000000003, "ルーム: {0}"),
        (37000000000004, "このリージョンは利用できません。"),
        (37000000000005, "リージョンを切り替え中..."),
        (37000000000006, "すでにこのリージョンです。"),
        (37000000000007, "接続中..."),
        (37000000000008, "リージョンを切り替えられませんでした。"),
        (37000000000009, "リージョンを切り替えました。"),
        (37000000000010, "ロビーに参加しました。"),
        (37000000000011, "ルームを作成できませんでした。"),
        (37000000000012, "パスワードが違います。"),
        (37000000000013, "ルームに参加できませんでした。"),
        (37000000000014, "ルーム名を入力してください。"),
        (37000000000015, "参加できるルームが見つかりません。")
    };

    public static void Run()
    {
        try
        {
            string characterSet = UpdateJapaneseStringTable();
            TMP_FontAsset japaneseFont = CreateJapaneseFontAsset(characterSet);
            int variantCount = AddJapaneseFontVariants(japaneseFont);
            int popupCount = AssignSkillPopupJapaneseFont(japaneseFont);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"CodexJapaneseLocalizationSetup complete. Entries={JapaneseEntries.Length}, Characters={characterSet.Length}, Variants={variantCount}, SkillPopups={popupCount}");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    private static string UpdateJapaneseStringTable()
    {
        StringTable table = AssetDatabase.LoadAssetAtPath<StringTable>(JapaneseTablePath);
        if (table == null)
            throw new InvalidOperationException($"Japanese string table was not found at {JapaneseTablePath}.");

        foreach ((long id, string text) in JapaneseEntries)
            table.AddEntry(id, text);

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();

        return BuildCharacterSet();
    }

    private static string BuildCharacterSet()
    {
        SortedSet<char> characters = new SortedSet<char>();
        foreach ((long _, string text) in JapaneseEntries)
        {
            foreach (char character in text)
            {
                if (!char.IsControl(character))
                    characters.Add(character);
            }
        }

        return new string(characters.ToArray());
    }

    private static TMP_FontAsset CreateJapaneseFontAsset(string characterSet)
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
            throw new InvalidOperationException($"Source font was not found at {SourceFontPath}.");

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JapaneseFontPath) != null)
            AssetDatabase.DeleteAsset(JapaneseFontPath);

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        if (fontAsset == null)
            throw new InvalidOperationException("Failed to create Yu Gothic UI TMP font asset.");

        fontAsset.name = "Yu Gothic UI SDF";
        fontAsset.material.name = "Yu Gothic UI SDF Material";
        fontAsset.atlasTextures[0].name = "Yu Gothic UI SDF Atlas";

        AssetDatabase.CreateAsset(fontAsset, JapaneseFontPath);
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        bool addedAllCharacters = fontAsset.TryAddCharacters(characterSet, out string missingCharacters);
        if (!addedAllCharacters && !string.IsNullOrEmpty(missingCharacters))
            Debug.LogWarning($"Yu Gothic UI SDF is missing these characters: {missingCharacters}");

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
        fontAsset.creationSettings = new FontAssetCreationSettings
        {
            sourceFontFileName = Path.GetFileName(SourceFontPath),
            sourceFontFileGUID = AssetDatabase.AssetPathToGUID(SourceFontPath),
            faceIndex = 0,
            pointSizeSamplingMode = 0,
            pointSize = 90,
            padding = 9,
            paddingMode = 2,
            packingMode = 0,
            atlasWidth = 2048,
            atlasHeight = 2048,
            characterSetSelectionMode = 7,
            characterSequence = characterSet,
            referencedFontAssetGUID = string.Empty,
            referencedTextAssetGUID = string.Empty,
            fontStyle = 0,
            fontStyleModifier = 0,
            renderMode = (int)GlyphRenderMode.SDFAA,
            includeFontFeatures = false
        };

        EditorUtility.SetDirty(fontAsset);
        EditorUtility.SetDirty(fontAsset.material);
        EditorUtility.SetDirty(fontAsset.atlasTextures[0]);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }

    private static int AddJapaneseFontVariants(TMP_FontAsset japaneseFont)
    {
        int count = 0;
        foreach (string scenePath in FindAssetPaths("t:Scene", SceneSearchFolders))
            count += AddJapaneseFontVariantsToScene(scenePath, japaneseFont);

        foreach (string prefabPath in FindAssetPaths("t:Prefab", PrefabSearchFolders))
            count += AddJapaneseFontVariantsToPrefab(prefabPath, japaneseFont);

        return count;
    }

    private static IEnumerable<string> FindAssetPaths(string filter, IReadOnlyList<string> folders)
    {
        string[] existingFolders = folders.Where(AssetDatabase.IsValidFolder).ToArray();
        if (existingFolders.Length == 0)
            return Enumerable.Empty<string>();

        return AssetDatabase.FindAssets(filter, existingFolders)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Where(path => !path.StartsWith("Assets/Photon/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.StartsWith("Assets/TextMesh Pro/Examples & Extras/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static int AddJapaneseFontVariantsToScene(string scenePath, TMP_FontAsset japaneseFont)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (GameObjectLocalizer localizer in root.GetComponentsInChildren<GameObjectLocalizer>(true))
            {
                if (AddJapaneseFontVariants(localizer, japaneseFont))
                    count++;
            }
        }

        if (count > 0)
            EditorSceneManager.SaveScene(scene);

        return count;
    }

    private static int AddJapaneseFontVariantsToPrefab(string prefabPath, TMP_FontAsset japaneseFont)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            int count = 0;
            foreach (GameObjectLocalizer localizer in root.GetComponentsInChildren<GameObjectLocalizer>(true))
            {
                if (AddJapaneseFontVariants(localizer, japaneseFont))
                    count++;
            }

            if (count > 0)
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            return count;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool AddJapaneseFontVariants(GameObjectLocalizer localizer, TMP_FontAsset japaneseFont)
    {
        bool changed = false;
        HashSet<TMP_Text> handledTargets = new HashSet<TMP_Text>();

        foreach (TrackedObject trackedObject in localizer.TrackedObjects)
        {
            if (trackedObject?.Target is TMP_Text tmpText)
            {
                handledTargets.Add(tmpText);
                changed |= AddJapaneseFontVariants(trackedObject, japaneseFont);
            }
        }

        foreach (TMP_Text tmpText in localizer.GetComponents<TMP_Text>())
        {
            if (!handledTargets.Add(tmpText))
                continue;

            TrackedObject trackedObject = localizer.GetTrackedObject(tmpText);
            if (trackedObject == null)
                trackedObject = tmpText is Graphic ? localizer.GetTrackedObject<TrackedUGuiGraphic>(tmpText) : localizer.GetTrackedObject<TrackedMonoBehaviourObject>(tmpText);

            changed |= AddJapaneseFontVariants(trackedObject, japaneseFont);
        }

        if (changed)
        {
            EditorUtility.SetDirty(localizer);
            EditorUtility.SetDirty(localizer.gameObject);
        }

        return changed;
    }

    private static bool AddJapaneseFontVariants(TrackedObject trackedObject, TMP_FontAsset japaneseFont)
    {
        bool changed = false;
        changed |= SetUnityObjectVariant(trackedObject, "m_fontAsset", japaneseFont, typeof(TMP_FontAsset));
        changed |= SetUnityObjectVariant(trackedObject, "m_sharedMaterial", japaneseFont.material, typeof(Material));
        return changed;
    }

    private static bool SetUnityObjectVariant(TrackedObject trackedObject, string propertyPath, UnityEngine.Object value, Type propertyType)
    {
        UnityObjectProperty property = trackedObject.GetTrackedProperty<UnityObjectProperty>(propertyPath);
        property.PropertyType = propertyType;

        if (property.GetValue(JapaneseLocale, out UnityEngine.Object existingValue) && existingValue == value)
            return false;

        property.SetValue(JapaneseLocale, value);
        return true;
    }

    private static int AssignSkillPopupJapaneseFont(TMP_FontAsset japaneseFont)
    {
        int count = 0;
        foreach (string scenePath in FindAssetPaths("t:Scene", SceneSearchFolders))
            count += AssignSkillPopupJapaneseFontInScene(scenePath, japaneseFont);

        foreach (string prefabPath in FindAssetPaths("t:Prefab", PrefabSearchFolders))
            count += AssignSkillPopupJapaneseFontInPrefab(prefabPath, japaneseFont);

        return count;
    }

    private static int AssignSkillPopupJapaneseFontInScene(string scenePath, TMP_FontAsset japaneseFont)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (CharacterSkillPopup popup in root.GetComponentsInChildren<CharacterSkillPopup>(true))
            {
                if (AssignSkillPopupJapaneseFont(popup, japaneseFont))
                    count++;
            }
        }

        if (count > 0)
            EditorSceneManager.SaveScene(scene);

        return count;
    }

    private static int AssignSkillPopupJapaneseFontInPrefab(string prefabPath, TMP_FontAsset japaneseFont)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            int count = 0;
            foreach (CharacterSkillPopup popup in root.GetComponentsInChildren<CharacterSkillPopup>(true))
            {
                if (AssignSkillPopupJapaneseFont(popup, japaneseFont))
                    count++;
            }

            if (count > 0)
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            return count;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool AssignSkillPopupJapaneseFont(CharacterSkillPopup popup, TMP_FontAsset japaneseFont)
    {
        SerializedObject serializedObject = new SerializedObject(popup);
        SerializedProperty fontProperty = serializedObject.FindProperty("localizedJapaneseFontAsset");
        if (fontProperty == null || fontProperty.objectReferenceValue == japaneseFont)
            return false;

        fontProperty.objectReferenceValue = japaneseFont;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(popup);
        EditorUtility.SetDirty(popup.gameObject);
        return true;
    }
}
