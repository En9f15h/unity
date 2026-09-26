using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CharacterSelectionSceneBuilder
{
    private const string DefinitionFolder = "Assets/Generated/CharacterSelection/Definitions";
    private const string PrefabFolder = "Assets/Generated/CharacterSelection/Prefabs";
    private const string CharacterSelectScenePath = "Assets/Scenes/CharacterSelectScene.unity";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string KnightDefinitionPath = DefinitionFolder + "/KnightSelectionDefinition.asset";
    private const string OracleDefinitionPath = DefinitionFolder + "/OracleSelectionDefinition.asset";
    private const string StormMapDefinitionPath = DefinitionFolder + "/Map_StormField.asset";
    private const string AstralMapDefinitionPath = DefinitionFolder + "/Map_AstralRuins.asset";
    private const string CharacterCardPrefabPath = PrefabFolder + "/CharacterSelectionCard.prefab";
    private const string MapCardPrefabPath = PrefabFolder + "/MapSelectionCard.prefab";
    private const string SkillButtonPrefabPath = PrefabFolder + "/SkillDisplayButton.prefab";
    private const string UIControllerPath = "Assets/Generated/CharacterSelection/Controllers/CharacterSelection_UI.controller";
    private const string HiddenCharacterSpritePath = "Assets/Resources/CharacterSelection/HiddenCharacter.png";

    [MenuItem("Tools/Character Selection/Build Scene And Data")]
    public static void BuildSceneAndData()
    {
        CharacterSelectionAnimationGenerator.GeneratePlaceholderAssets();
        CharacterSelectionTextureGenerator.EnsureFolder(DefinitionFolder);
        CharacterSelectionTextureGenerator.EnsureFolder(PrefabFolder);
        CharacterSelectionUiPreviewAndRadarIconBuilder.EnsureRadarIconAssets();

        CharacterSelectionDefinition[] characters = EnsureGeneratedCharacterReferences(CreateCharacterDefinitions());
        CharacterSelectionUiPreviewAndRadarIconBuilder.GenerateUiPreviewPrefabs(characters);
        MapSelectionDefinition[] maps = EnsureGeneratedMapReferences(CreateMapDefinitions());
        GameObject characterCardPrefab = GetLiveOrLoad(CreateCharacterCardPrefab(), CharacterCardPrefabPath);
        GameObject mapCardPrefab = GetLiveOrLoad(CreateMapCardPrefab(), MapCardPrefabPath);
        GameObject skillButtonPrefab = GetLiveOrLoad(CreateSkillButtonPrefab(), SkillButtonPrefabPath);

        CreateCharacterSelectScene(characters, maps, skillButtonPrefab);
        InstallGameSceneLoader(characters, maps);
        AddSceneToBuildSettings(CharacterSelectScenePath);
        AddSceneToBuildSettings(GameScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Character Selection scene and data generated. Compatibility prefabs: " + characterCardPrefab.name + ", " + mapCardPrefab.name);
    }

    public static void BuildFromCommandLine()
    {
        BuildSceneAndData();
        EditorApplication.Exit(0);
    }

    private static CharacterSelectionDefinition[] CreateCharacterDefinitions()
    {
        CharacterClassConfig knightConfig = AssetDatabase.LoadAssetAtPath<CharacterClassConfig>("Assets/Scripts/game/classes/KnightConfig.asset");
        CharacterClassConfig oracleConfig = AssetDatabase.LoadAssetAtPath<CharacterClassConfig>("Assets/Scripts/game/classes/OracleConfig.asset");
        Sprite knightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Character/Knight/knight.png");
        Sprite oracleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Character/Oracle/Oracle.png");

        CharacterSelectionDefinition knight = CreateCharacterDefinition(
            "KnightSelectionDefinition",
            "knight",
            "Knight",
            "Knight",
            "Balanced Duelist",
            "Balanced Duelist",
            "A balanced melee fighter with steady defense, reliable movement, and direct pressure.",
            knightConfig,
            0,
            knightSprite,
            5, 4, 4, 3, 2, 2,
            2);

        CharacterSelectionDefinition oracle = CreateCharacterDefinition(
            "OracleSelectionDefinition",
            "oracle",
            "Oracle",
            "Oracle",
            "Prediction Caster",
            "Prediction Caster",
            "A prediction caster who controls spacing, reveals intent, and punishes enemy plans.",
            oracleConfig,
            1,
            oracleSprite,
            3, 3, 3, 4, 5, 5,
            4);

        return new[] { knight, oracle };
    }

    private static CharacterSelectionDefinition CreateCharacterDefinition(
        string assetName,
        string characterId,
        string displayName,
        string englishName,
        string roleName,
        string shortRole,
        string description,
        CharacterClassConfig config,
        int legacyClassIndex,
        Sprite sprite,
        int vitality,
        int attack,
        int defense,
        int mobility,
        int range,
        int prediction,
        int difficulty)
    {
        string path = DefinitionFolder + "/" + assetName + ".asset";
        CharacterSelectionDefinition definition = AssetDatabase.LoadAssetAtPath<CharacterSelectionDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<CharacterSelectionDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        definition.characterId = characterId;
        definition.displayName = displayName;
        definition.englishName = englishName;
        definition.roleName = roleName;
        definition.shortRole = shortRole;
        definition.description = description;
        definition.combatConfig = config;
        definition.legacyClassIndex = legacyClassIndex;
        definition.cardSprite = sprite;
        definition.portraitSprite = sprite;
        definition.previewPrefab = GetSkinPrefab(config, 0);
        definition.vitality = vitality;
        definition.attack = attack;
        definition.defense = defense;
        definition.mobility = mobility;
        definition.range = range;
        definition.prediction = prediction;
        definition.vitalityRating = vitality;
        definition.attackRating = attack;
        definition.defenseRating = defense;
        definition.mobilityRating = mobility;
        definition.rangeRating = range;
        definition.predictionRating = prediction;
        definition.difficultyRating = difficulty;
        definition.skinNames = config != null && config.skinNames != null ? config.skinNames : new[] { "Default" };
        definition.skinPortraits = BuildSkinSpriteArray(config, sprite);
        definition.skinPreviewPrefabs = config != null && config.skinPrefabs != null ? config.skinPrefabs : new GameObject[0];
        definition.skills = BuildSkillDisplayData(config);
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static SkillDisplayData[] BuildSkillDisplayData(CharacterClassConfig config)
    {
        if (config == null)
            return new SkillDisplayData[0];

        ActionData[] actions = config.GetClassActions();
        List<SkillDisplayData> result = new List<SkillDisplayData>();
        for (int i = 0; i < actions.Length; i++)
        {
            ActionData action = actions[i];
            if (action == null)
                continue;

            SkillDisplayData display = new SkillDisplayData
            {
                skillName = string.IsNullOrEmpty(action.tooltipTitle) ? action.actionName : action.tooltipTitle,
                description = action.tooltipDescription,
                icon = action.iconSprite,
                range = action.rangeText,
                actionSlotCost = action.GetSlotCost(),
                energyCost = action is UltimateActionData ? 5 : 0,
                counterDescription = "Check timing, range, and counter rules before confirming."
            };

            if (action is AttackActionData attack)
            {
                display.damage = attack.damage;
                display.range = string.IsNullOrEmpty(display.range) ? attack.minRange + "-" + attack.range : display.range;
            }
            else if (action is UltimateActionData ultimate)
            {
                display.damage = ultimate.damage;
                display.range = string.IsNullOrEmpty(display.range) ? ultimate.minRange + "-" + ultimate.range : display.range;
            }

            result.Add(display);
        }

        return result.ToArray();
    }

    private static Sprite[] BuildSkinSpriteArray(CharacterClassConfig config, Sprite fallback)
    {
        int count = config != null && config.skinPrefabs != null && config.skinPrefabs.Length > 0 ? config.skinPrefabs.Length : 1;
        Sprite[] sprites = new Sprite[count];
        for (int i = 0; i < sprites.Length; i++)
            sprites[i] = fallback;

        return sprites;
    }

    private static GameObject GetSkinPrefab(CharacterClassConfig config, int index)
    {
        if (config == null || config.skinPrefabs == null || index < 0 || index >= config.skinPrefabs.Length)
            return null;

        return config.skinPrefabs[index];
    }

    private static MapSelectionDefinition[] CreateMapDefinitions()
    {
        Sprite softGlow = AssetDatabase.LoadAssetAtPath<Sprite>(CharacterSelectionTextureGenerator.GetPath("SoftGlow"));
        Sprite vignette = AssetDatabase.LoadAssetAtPath<Sprite>(CharacterSelectionTextureGenerator.GetPath("Vignette"));

        GameObject ruinsPrefab = CreateMapPrefab("Map_AstralRuins", new Color(0.13f, 0.09f, 0.20f), new Color(0.20f, 0.16f, 0.09f));
        GameObject stormPrefab = CreateMapPrefab("Map_StormField", new Color(0.06f, 0.12f, 0.18f), new Color(0.08f, 0.18f, 0.14f));

        MapSelectionDefinition ruins = CreateMapDefinition("Map_AstralRuins", "astral_ruins", "Astral Ruins", "Moonlit ruins with a gold-lit stone floor.", softGlow, ruinsPrefab, 0);
        MapSelectionDefinition storm = CreateMapDefinition("Map_StormField", "storm_field", "Storm Field", "A rain-dark field with an emerald fighting floor.", vignette, stormPrefab, 1);
        return new[] { ruins, storm };
    }

    private static MapSelectionDefinition CreateMapDefinition(string assetName, string mapId, string displayName, string description, Sprite preview, GameObject prefab, int legacyStageIndex)
    {
        string path = DefinitionFolder + "/" + assetName + ".asset";
        MapSelectionDefinition definition = AssetDatabase.LoadAssetAtPath<MapSelectionDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<MapSelectionDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        definition.mapId = mapId;
        definition.displayName = displayName;
        definition.description = description;
        definition.previewSprite = preview;
        definition.mapPrefab = prefab;
        definition.isUnlocked = true;
        definition.legacyStageIndex = legacyStageIndex;
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static GameObject CreateMapPrefab(string name, Color backgroundColor, Color floorColor)
    {
        GameObject root = new GameObject(name);

        GameObject background = new GameObject("Background");
        background.transform.SetParent(root.transform, false);
        SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = CreateSolidSprite(name + "_BackgroundSprite", backgroundColor);
        backgroundRenderer.sortingOrder = -20;
        background.transform.localScale = new Vector3(32f, 14f, 1f);

        GameObject floor = new GameObject("Floor");
        floor.transform.SetParent(root.transform, false);
        floor.transform.localPosition = new Vector3(0f, -3.2f, 0f);
        SpriteRenderer floorRenderer = floor.AddComponent<SpriteRenderer>();
        floorRenderer.sprite = CreateSolidSprite(name + "_FloorSprite", floorColor);
        floorRenderer.sortingOrder = -10;
        floor.transform.localScale = new Vector3(32f, 2.2f, 1f);

        string path = PrefabFolder + "/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            AssetDatabase.DeleteAsset(path);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Sprite CreateSolidSprite(string name, Color color)
    {
        string path = PrefabFolder + "/" + name + ".png";
        Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
                texture.SetPixel(x, y, color);
        }

        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject CreateCharacterCardPrefab()
    {
        GameObject root = CreatePanel("CharacterSelectionCard", new Vector2(250f, 310f), new Color(0.11f, 0.12f, 0.17f, 0.96f));
        CharacterSelectionCard card = root.AddComponent<CharacterSelectionCard>();
        Animator animator = AddAnimator(root, LoadGeneratedUIController());
        EnsureCanvasGroup(root);
        LayoutElement layoutElement = root.AddComponent<LayoutElement>();
        layoutElement.minWidth = 220f;
        layoutElement.preferredWidth = 250f;
        layoutElement.preferredHeight = 310f;

        Image background = root.GetComponent<Image>();
        background.raycastTarget = true;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        Image portrait = AddImage(root.transform, "Portrait", new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(190f, 145f), null, Color.white);
        TMP_Text nameText = AddText(root.transform, "CharacterNameText", "", 22, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(220f, 32f));
        TMP_Text roleText = AddText(root.transform, "RoleText", "", 16, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(220f, 24f));
        TMP_Text difficultyText = AddText(root.transform, "DifficultyText", "", 14, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(220f, 24f));
        GameObject youMarker = AddBadge(root.transform, "YOU", new Vector2(0f, 1f), new Vector2(34f, -22f), new Color(0.20f, 0.65f, 1f, 0.95f));
        GameObject enemyMarker = AddBadge(root.transform, "ENEMY", new Vector2(1f, 1f), new Vector2(-42f, -22f), new Color(0.85f, 0.25f, 0.35f, 0.95f));
        GameObject lockOverlay = AddOverlay(root.transform, "LockOverlay", new Color(0f, 0f, 0f, 0.55f), "READY");

        SetRef(card, "portraitImage", portrait);
        SetRef(card, "characterNameText", nameText);
        SetRef(card, "roleText", roleText);
        SetRef(card, "difficultyText", difficultyText);
        SetRef(card, "youMarker", youMarker);
        SetRef(card, "enemyMarker", enemyMarker);
        SetRef(card, "lockOverlay", lockOverlay);
        SetRef(card, "backgroundImage", background);
        SetRef(card, "button", button);
        SetRef(card, "animator", animator);

        youMarker.SetActive(false);
        enemyMarker.SetActive(false);
        lockOverlay.SetActive(false);
        SavePrefab(root, CharacterCardPrefabPath);
        return AssetDatabase.LoadAssetAtPath<GameObject>(CharacterCardPrefabPath);
    }

    private static GameObject CreateMapCardPrefab()
    {
        GameObject root = CreatePanel("MapSelectionCard", new Vector2(260f, 180f), new Color(0.09f, 0.10f, 0.15f, 0.96f));
        MapSelectionCard card = root.AddComponent<MapSelectionCard>();
        Animator animator = AddAnimator(root, LoadGeneratedUIController());
        EnsureCanvasGroup(root);
        LayoutElement layoutElement = root.AddComponent<LayoutElement>();
        layoutElement.minWidth = 210f;
        layoutElement.preferredWidth = 260f;
        layoutElement.preferredHeight = 180f;

        Image background = root.GetComponent<Image>();
        background.raycastTarget = true;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        Image preview = AddImage(root.transform, "Preview", new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(220f, 84f), null, Color.white);
        TMP_Text nameText = AddText(root.transform, "MapNameText", "", 18, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(230f, 24f));
        TMP_Text descText = AddText(root.transform, "DescriptionText", "", 12, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(230f, 34f));
        GameObject selected = AddBadge(root.transform, "SELECT", new Vector2(1f, 1f), new Vector2(-52f, -20f), new Color(0.95f, 0.72f, 0.25f, 0.95f));
        GameObject locked = AddOverlay(root.transform, "LockedOverlay", new Color(0f, 0f, 0f, 0.55f), string.Empty);

        SetRef(card, "previewImage", preview);
        SetRef(card, "mapNameText", nameText);
        SetRef(card, "descriptionText", descText);
        SetRef(card, "selectedMarker", selected);
        SetRef(card, "lockedOverlay", locked);
        SetRef(card, "backgroundImage", background);
        SetRef(card, "button", button);
        SetRef(card, "animator", animator);

        selected.SetActive(false);
        locked.SetActive(false);
        SavePrefab(root, MapCardPrefabPath);
        return AssetDatabase.LoadAssetAtPath<GameObject>(MapCardPrefabPath);
    }

    private static GameObject CreateSkillButtonPrefab()
    {
        GameObject root = CreatePanel("SkillDisplayButton", new Vector2(50f, 50f), new Color(0.11f, 0.13f, 0.18f, 0.95f));
        Image background = root.GetComponent<Image>();
        background.raycastTarget = true;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;

        SkillDisplayButton skillButton = root.AddComponent<SkillDisplayButton>();
        Image icon = AddImage(root.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f), null, Color.white);
        TMP_Text label = AddText(root.transform, "NameText", "", 10, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(48f, 14f));
        label.gameObject.SetActive(false);
        SetRef(skillButton, "iconImage", icon);
        SetRef(skillButton, "nameText", label);

        SavePrefab(root, SkillButtonPrefabPath);
        return AssetDatabase.LoadAssetAtPath<GameObject>(SkillButtonPrefabPath);
    }

    private static void CreateCharacterSelectScene(CharacterSelectionDefinition[] characters, MapSelectionDefinition[] maps, GameObject skillButtonPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RuntimeAnimatorController uiController = LoadGeneratedUIController();

        CreateCamera();
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("CharacterSelectCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        CharacterSelectionUILayoutController layout = canvasObject.AddComponent<CharacterSelectionUILayoutController>();

        CreateBackground(canvasObject.transform);
        Button backButton;
        TMP_Text statusText;
        CharacterSelectionPrivacyController privacyController = CreateTopBar(canvasObject.transform, out backButton, out statusText);

        CharacterSelectorController characterSelector;
        CharacterSelectionPlayerPanel youPanel = CreatePlayerPanel(canvasObject.transform, "YouPanel", "YOU", true, uiController, out characterSelector);
        CharacterSelectionPlayerPanel enemyPanel = CreatePlayerPanel(canvasObject.transform, "EnemyPanel", "ENEMY", false, uiController, out _);

        CharacterSelectionAnimationController centerAnimation = CreateCenterPanel(canvasObject.transform, uiController);
        MapSelectorController mapSelector = CreateMapSelector(canvasObject.transform, maps, uiController);
        CharacterSkillPopup youSkillPopup = CreateSkillPopup(canvasObject.transform, "SkillPopup", new Vector2(0.5f, 0f), new Vector2(-235f, 274f), skillButtonPrefab);
        CharacterSkillPopup enemySkillPopup = CreateSkillPopup(canvasObject.transform, "EnemySkillPopup", new Vector2(0.5f, 0f), new Vector2(235f, 274f), skillButtonPrefab);

        TMP_Text revealText;
        TMP_Text countdownText;
        TMP_Text fightText;
        GameObject phaseOverlay = CreatePhaseOverlay(canvasObject.transform, out revealText, out countdownText, out fightText);
        CharacterSelectionAnimationController sceneFade = CreateSceneFade(canvasObject.transform, uiController);

        SetRef(characterSelector, "skillPopup", youSkillPopup);

        GameObject managerObject = new GameObject("CharacterSelectionManager");
        CharacterSelectionManager manager = managerObject.AddComponent<CharacterSelectionManager>();
        SetArray(manager, "characters", characters);
        SetArray(manager, "maps", maps);
        SetString(manager, "battleSceneName", "GameScene");
        SetRef(manager, "characterSelector", characterSelector);
        SetRef(manager, "mapSelectorController", mapSelector);
        SetRef(manager, "youPanel", youPanel);
        SetRef(manager, "enemyPanel", enemyPanel);
        SetRef(manager, "privacyController", privacyController);
        SetBool(manager, "defaultPublicSelection", false);
        SetRef(manager, "youSkillPopup", youSkillPopup);
        SetRef(manager, "enemySkillPopup", enemySkillPopup);
        SetRef(manager, "backButton", backButton);
        SetRef(manager, "statusText", statusText);
        SetRef(manager, "phaseOverlay", phaseOverlay);
        SetRef(manager, "revealText", revealText);
        SetRef(manager, "countdownText", countdownText);
        SetRef(manager, "fightText", fightText);
        SetRef(manager, "selectedMapText", null);
        SetFloat(manager, "revealDuration", 1.5f);
        SetFloat(manager, "countdownDuration", 3f);
        SetRef(manager, "sceneFade", sceneFade);
        SetRef(manager, "vsAnimation", centerAnimation);

        SetRef(layout, "topBar", FindChildRect(canvasObject.transform, "TopBar"));
        SetRef(layout, "youPanel", youPanel.transform as RectTransform);
        SetRef(layout, "enemyPanel", enemyPanel.transform as RectTransform);
        SetRef(layout, "centerPanel", FindChildRect(canvasObject.transform, "CenterPanel"));
        SetRef(layout, "mapSelectorPanel", mapSelector.transform as RectTransform);
        SetRef(layout, "youSkillPopup", youSkillPopup.transform as RectTransform);
        SetRef(layout, "enemySkillPopup", enemySkillPopup.transform as RectTransform);
        SetRef(layout, "phaseOverlay", phaseOverlay.transform as RectTransform);
        SetRef(layout, "sceneFade", sceneFade.transform as RectTransform);
        layout.ApplyLayout();
        SelectSceneUiImageBinder.ApplyToActiveScene();

        EditorSceneManager.SaveScene(scene, CharacterSelectScenePath);
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("CharacterSelectCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
    }

    private static void EnsureEventSystem()
    {
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    private static void CreateBackground(Transform parent)
    {
        GameObject root = CreateAnchoredPanel(parent, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.025f, 0.035f, 1f));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        AddFullImage(root.transform, "BlueNightBand", new Color(0.02f, 0.055f, 0.085f, 0.72f));
        Sprite fog = AssetDatabase.LoadAssetAtPath<Sprite>(CharacterSelectionTextureGenerator.GetPath("PurpleFogNoise"));
        Image leftFog = AddImage(root.transform, "PurpleFogLeft", new Vector2(0f, 0.5f), new Vector2(250f, 0f), new Vector2(620f, 900f), fog, new Color(0.34f, 0.18f, 0.52f, 0.16f));
        Image rightFog = AddImage(root.transform, "PurpleFogRight", new Vector2(1f, 0.5f), new Vector2(-250f, 0f), new Vector2(620f, 900f), fog, new Color(0.12f, 0.45f, 0.58f, 0.12f));
        leftFog.type = Image.Type.Tiled;
        rightFog.type = Image.Type.Tiled;
        AddFullImage(root.transform, "Vignette", new Color(0f, 0f, 0f, 0.28f));
    }

    private static CharacterSelectionPrivacyController CreateTopBar(Transform parent, out Button backButton, out TMP_Text statusText)
    {
        GameObject root = CreateAnchoredPanel(parent, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 88f), new Color(0.035f, 0.04f, 0.055f, 0.95f));
        backButton = CreateButton(root.transform, "BackButton", "<", new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(64f, 38f));
        statusText = AddText(root.transform, "StatusText", "SELECT", 26, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 40f));

        Toggle revealToggle = CreateToggle(root.transform, "PublicSelectionToggle", string.Empty, new Vector2(1f, 0.5f), new Vector2(-126f, 0f));

        CharacterSelectionPrivacyController controller = root.AddComponent<CharacterSelectionPrivacyController>();
        SetRef(controller, "publicSelectionToggle", revealToggle);
        SetRef(controller, "statusText", null);
        return controller;
    }

    private static CharacterSelectionPlayerPanel CreatePlayerPanel(Transform parent, string name, string title, bool localPanel, RuntimeAnimatorController uiController, out CharacterSelectorController selector)
    {
        GameObject root = CreateAnchoredPanel(parent, name, localPanel ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f), localPanel ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f), Vector2.zero, new Vector2(400f, 820f), new Color(0.04f, 0.075f, 0.095f, 0.92f));
        CharacterSelectionPlayerPanel panel = root.AddComponent<CharacterSelectionPlayerPanel>();
        AddAnimator(root, uiController);
        EnsureCanvasGroup(root);

        TMP_Text titleText = AddText(root.transform, title + "Text", title, 28, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(260f, 42f));
        Image crown = AddImage(root.transform, "CrownImage", new Vector2(1f, 1f), new Vector2(-34f, -34f), new Vector2(34f, 34f), AssetDatabase.LoadAssetAtPath<Sprite>(CharacterSelectionTextureGenerator.GetPath("SoftGlow")), new Color(0.95f, 0.72f, 0.24f, 0.92f));
        TMP_Text readyText = AddText(root.transform, "ReadyStateText", "SELECT", 18, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(180f, 30f));

        GameObject previewRoot = CreateAnchoredPanel(root.transform, "CharacterPreviewRoot", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(260f, 230f), new Color(0.05f, 0.055f, 0.075f, 0.86f));
        CharacterPreviewController preview = previewRoot.AddComponent<CharacterPreviewController>();
        Image portrait = AddImage(previewRoot.transform, "PortraitFallback", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 170f), null, Color.white);
        portrait.preserveAspect = true;
        GameObject silhouette = AddOverlay(previewRoot.transform, "HiddenSilhouette", new Color(0.12f, 0.12f, 0.17f, 0.82f), "?");
        SetRef(preview, "previewRoot", previewRoot.transform);
        SetRef(preview, "portraitFallbackImage", portrait);
        SetRef(preview, "hiddenPortraitSprite", AssetDatabase.LoadAssetAtPath<Sprite>(HiddenCharacterSpritePath));
        SetRef(preview, "hiddenSilhouette", silhouette);

        GameObject infoRoot = CreateAnchoredPanel(root.transform, "CharacterInfo", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(350f, 150f), new Color(0f, 0f, 0f, 0f));
        TMP_Text characterName = AddText(infoRoot.transform, "CharacterNameText", "SELECT", 24, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(330f, 32f));
        TMP_Text role = AddText(infoRoot.transform, "RoleText", "", 16, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(330f, 24f));
        TMP_Text description = AddText(infoRoot.transform, "DescriptionText", "", 13, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(330f, 62f));
        TMP_Text skin = AddText(infoRoot.transform, "SkinText", "", 14, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(330f, 24f));
        role.gameObject.SetActive(false);
        description.gameObject.SetActive(false);
        skin.gameObject.SetActive(false);

        GameObject hiddenRoot = CreateAnchoredPanel(root.transform, "HiddenSelectionRoot", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -414f), new Vector2(350f, 100f), new Color(0f, 0f, 0f, 0f));
        TMP_Text hiddenText = AddText(hiddenRoot.transform, "HiddenSelectionText", localPanel ? "SELECT" : "ENEMY", 26, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330f, 52f));

        Image radarBackground = AddImage(root.transform, "RadarBackgroundImage", new Vector2(0.5f, 0f), new Vector2(0f, 184f), new Vector2(260f, 220f), null, Color.white);
        radarBackground.gameObject.SetActive(false);

        GameObject radarRoot = CreateAnchoredPanel(root.transform, "RadarChartRoot", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 184f), new Vector2(260f, 220f), new Color(0f, 0f, 0f, 0f));
        Object.DestroyImmediate(radarRoot.GetComponent<Image>());
        HexRadarChartGraphic radar = radarRoot.AddComponent<HexRadarChartGraphic>();
        CharacterRadarChartController radarController = radarRoot.AddComponent<CharacterRadarChartController>();
        Animator radarAnimator = AddAnimator(radarRoot, uiController);
        EnsureCanvasGroup(radarRoot);
        TMP_Text[] labels = CreateRadarLabels(radarRoot.transform);
        SetRef(radarController, "chart", radar);
        SetRef(radarController, "backgroundImage", radarBackground);
        SetRef(radarController, "animator", radarAnimator);
        SetArray(radarController, "axisLabels", labels);
        CharacterSelectionUiPreviewAndRadarIconBuilder.PatchRadarIcons(radarController);

        Button previousCharacter = null;
        Button nextCharacter = null;
        Button previousSkin = null;
        Button nextSkin = null;
        Button confirm = null;
        Button cancel = null;

        if (localPanel)
        {
            characterName.rectTransform.sizeDelta = new Vector2(220f, 32f);
            AddText(root.transform, "SkinHeaderText", "SKIN", 18, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 318f), new Vector2(96f, 28f));
            previousCharacter = CreateButton(root.transform, "PreviousCharacterButton", "<", new Vector2(0.5f, 1f), new Vector2(-156f, -428f), new Vector2(42f, 36f));
            nextCharacter = CreateButton(root.transform, "NextCharacterButton", ">", new Vector2(0.5f, 1f), new Vector2(156f, -428f), new Vector2(42f, 36f));
            previousSkin = CreateButton(root.transform, "PreviousSkinButton", "<", new Vector2(0.5f, 0f), new Vector2(-112f, 318f), new Vector2(44f, 36f));
            nextSkin = CreateButton(root.transform, "NextSkinButton", ">", new Vector2(0.5f, 0f), new Vector2(112f, 318f), new Vector2(44f, 36f));
            confirm = CreateButton(root.transform, "ConfirmButton", "SELECT", new Vector2(0.5f, 0f), new Vector2(-82f, 36f), new Vector2(150f, 48f));
            cancel = CreateButton(root.transform, "CancelReadyButton", "CANCEL", new Vector2(0.5f, 0f), new Vector2(92f, 36f), new Vector2(150f, 48f));
        }

        GameObject readyEffect = AddOverlay(root.transform, "ReadyEffectRoot", new Color(0.22f, 0.72f, 1f, 0.14f), string.Empty);
        readyEffect.SetActive(false);

        SetRef(panel, "perspectiveTitleText", titleText);
        SetRef(panel, "crownImage", crown);
        SetRef(panel, "readyStateText", readyText);
        SetRef(panel, "characterNameText", characterName);
        SetRef(panel, "roleText", role);
        SetRef(panel, "descriptionText", description);
        SetRef(panel, "skinText", skin);
        SetRef(panel, "characterInfoRoot", infoRoot);
        SetRef(panel, "hiddenSelectionRoot", hiddenRoot);
        SetRef(panel, "hiddenSelectionText", hiddenText);
        SetRef(panel, "characterPreviewRoot", previewRoot);
        SetRef(panel, "radarChartRoot", radarRoot);
        SetRef(panel, "previewController", preview);
        SetRef(panel, "radarChartController", radarController);
        SetRef(panel, "previousCharacterButton", previousCharacter);
        SetRef(panel, "nextCharacterButton", nextCharacter);
        SetRef(panel, "previousSkinButton", previousSkin);
        SetRef(panel, "nextSkinButton", nextSkin);
        SetRef(panel, "confirmButton", confirm);
        SetRef(panel, "confirmButtonText", confirm != null ? confirm.GetComponentInChildren<TMP_Text>(true) : null);
        SetRef(panel, "cancelReadyButton", cancel);
        SetRef(panel, "readyEffectRoot", readyEffect);
        SetRef(panel, "panelBackgroundImage", root.GetComponent<Image>());
        SetRef(panel, "characterPreviewBackgroundImage", previewRoot.GetComponent<Image>());
        SetRef(panel, "characterInfoBackgroundImage", infoRoot.GetComponent<Image>());

        selector = null;
        if (localPanel)
        {
            selector = root.AddComponent<CharacterSelectorController>();
            SetRef(selector, "targetPanel", panel);
            SetRef(selector, "previousCharacterButton", previousCharacter);
            SetRef(selector, "nextCharacterButton", nextCharacter);
            SetRef(selector, "previousSkinButton", previousSkin);
            SetRef(selector, "nextSkinButton", nextSkin);
        }

        hiddenRoot.SetActive(!localPanel);
        crown.gameObject.SetActive(false);
        if (cancel != null)
            cancel.gameObject.SetActive(false);

        return panel;
    }

    private static CharacterSelectionAnimationController CreateCenterPanel(Transform parent, RuntimeAnimatorController uiController)
    {
        GameObject root = CreateAnchoredPanel(parent, "CenterPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 92f), new Vector2(180f, 100f), new Color(0f, 0f, 0f, 0f));
        CharacterSelectionAnimationController animation = AddAnimationController(root, uiController);
        AddText(root.transform, "VSText", "VS", 42, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 70f));
        Image flash = AddImage(root.transform, "LightningImage", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210f, 32f), AssetDatabase.LoadAssetAtPath<Sprite>(CharacterSelectionTextureGenerator.GetPath("LightningStrip")), new Color(0.72f, 0.95f, 1f, 0.44f));
        flash.transform.SetAsFirstSibling();
        return animation;
    }

    private static MapSelectorController CreateMapSelector(Transform parent, MapSelectionDefinition[] maps, RuntimeAnimatorController uiController)
    {
        GameObject root = CreateAnchoredPanel(parent, "MapSelector", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(640f, 188f), new Color(0.04f, 0.045f, 0.065f, 0.88f));
        MapSelectorController selector = root.AddComponent<MapSelectorController>();
        CharacterSelectionAnimationController animation = AddAnimationController(root, uiController);

        Button previous = CreateButton(root.transform, "PreviousMapButton", "<", new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(58f, 58f));
        Button next = CreateButton(root.transform, "NextMapButton", ">", new Vector2(1f, 0.5f), new Vector2(-44f, 0f), new Vector2(58f, 58f));
        Image preview = AddImage(root.transform, "MapPreviewImage", new Vector2(0.5f, 0.62f), new Vector2(0f, 8f), new Vector2(390f, 104f), maps != null && maps.Length > 0 && maps[0] != null ? maps[0].previewSprite : null, Color.white);
        TMP_Text mapName = AddText(root.transform, "CurrentMapNameText", maps != null && maps.Length > 0 && maps[0] != null ? maps[0].displayName : string.Empty, 20, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(430f, 30f));
        GameObject lockImage = AddOverlay(root.transform, "HostOnlyLockImage", new Color(0f, 0f, 0f, 0.35f), string.Empty);
        lockImage.SetActive(false);

        SetRef(selector, "previousMapButton", previous);
        SetRef(selector, "nextMapButton", next);
        SetRef(selector, "mapPreviewImage", preview);
        SetRef(selector, "currentMapNameText", mapName);
        SetRef(selector, "hostOnlyLockImage", lockImage);
        SetRef(selector, "animationController", animation);
        return selector;
    }

    private static CharacterSkillPopup CreateSkillPopup(Transform parent, string name, Vector2 anchor, Vector2 position, GameObject buttonPrefab)
    {
        GameObject root = CreateAnchoredPanel(parent, name, anchor, anchor, position, new Vector2(450f, 230f), new Color(0.04f, 0.045f, 0.07f, 0.94f));
        CharacterSkillPopup popup = root.AddComponent<CharacterSkillPopup>();

        GameObject buttonRoot = CreateAnchoredPanel(root.transform, "ButtonRoot", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(112f, 206f), new Color(0f, 0f, 0f, 0f));
        GridLayoutGroup buttonLayout = buttonRoot.AddComponent<GridLayoutGroup>();
        buttonLayout.cellSize = new Vector2(50f, 50f);
        buttonLayout.spacing = new Vector2(6f, 6f);
        buttonLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        buttonLayout.constraintCount = 2;
        buttonLayout.childAlignment = TextAnchor.UpperLeft;

        Image icon = AddImage(root.transform, "SkillIcon", new Vector2(0f, 1f), new Vector2(168f, -40f), new Vector2(54f, 54f), null, Color.white);
        TMP_Text skillName = AddText(root.transform, "SkillNameText", "", 18, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(284f, -28f), new Vector2(210f, 30f));
        TMP_Text desc = AddText(root.transform, "DescriptionText", "", 12, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(302f, -84f), new Vector2(250f, 68f));
        TMP_Text dmg = AddText(root.transform, "DamageText", "", 11, TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(168f, 62f), new Vector2(98f, 22f));
        TMP_Text range = AddText(root.transform, "RangeText", "", 11, TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(282f, 62f), new Vector2(120f, 22f));
        TMP_Text slot = AddText(root.transform, "SlotCostText", "", 11, TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(168f, 36f), new Vector2(100f, 22f));
        TMP_Text energy = AddText(root.transform, "EnergyCostText", "", 11, TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(282f, 36f), new Vector2(124f, 22f));
        TMP_Text counter = AddText(root.transform, "CounterText", "", 10, TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(302f, 12f), new Vector2(250f, 28f));

        SetRef(popup, "buttonRoot", buttonRoot.transform);
        SetRef(popup, "buttonPrefab", buttonPrefab);
        SetRef(popup, "popupRoot", root);
        SetRef(popup, "skillIcon", icon);
        SetRef(popup, "skillNameText", skillName);
        SetRef(popup, "descriptionText", desc);
        SetRef(popup, "damageText", dmg);
        SetRef(popup, "rangeText", range);
        SetRef(popup, "slotCostText", slot);
        SetRef(popup, "energyCostText", energy);
        SetRef(popup, "counterText", counter);
        return popup;
    }

    private static GameObject CreatePhaseOverlay(Transform parent, out TMP_Text revealText, out TMP_Text countdownText, out TMP_Text fightText)
    {
        GameObject overlay = CreateAnchoredPanel(parent, "PhaseOverlay", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.58f));
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().raycastTarget = true;

        revealText = AddText(overlay.transform, "RevealText", "REVEAL", 84, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(460f, 120f));
        countdownText = AddText(overlay.transform, "CountdownText", "3", 120, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 150f));
        fightText = AddText(overlay.transform, "FightText", "FIGHT", 96, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(420f, 120f));
        overlay.SetActive(false);
        return overlay;
    }

    private static CharacterSelectionAnimationController CreateSceneFade(Transform parent, RuntimeAnimatorController uiController)
    {
        GameObject root = CreateAnchoredPanel(parent, "SceneFade", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().raycastTarget = false;
        return AddAnimationController(root, uiController);
    }

    private static void InstallGameSceneLoader(CharacterSelectionDefinition[] characters, MapSelectionDefinition[] maps)
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        characters = EnsureGeneratedCharacterReferences(characters);
        maps = EnsureGeneratedMapReferences(maps);

        SelectedBattleSetupLoader loader = FindSceneComponent<SelectedBattleSetupLoader>(scene);
        if (loader == null)
        {
            GameObject obj = new GameObject("SelectedBattleSetupLoader");
            loader = obj.AddComponent<SelectedBattleSetupLoader>();
        }

        Transform mapRoot = loader.transform.Find("SelectedMapRoot");
        if (mapRoot == null)
        {
            GameObject mapRootObject = new GameObject("SelectedMapRoot");
            mapRootObject.transform.SetParent(loader.transform, false);
            mapRoot = mapRootObject.transform;
        }

        SetArray(loader, "characters", characters);
        SetArray(loader, "maps", maps);
        SetRef(loader, "mapRoot", mapRoot);
        EditorSceneManager.SaveScene(scene);
    }

    private static CharacterSelectionDefinition[] EnsureGeneratedCharacterReferences(CharacterSelectionDefinition[] current)
    {
        return new[]
        {
            GetLiveOrLoad(GetArrayValue(current, 0), KnightDefinitionPath),
            GetLiveOrLoad(GetArrayValue(current, 1), OracleDefinitionPath)
        };
    }

    private static MapSelectionDefinition[] EnsureGeneratedMapReferences(MapSelectionDefinition[] current)
    {
        return new[]
        {
            GetLiveOrLoad(GetArrayValue(current, 0), AstralMapDefinitionPath),
            GetLiveOrLoad(GetArrayValue(current, 1), StormMapDefinitionPath)
        };
    }

    private static T GetArrayValue<T>(T[] values, int index) where T : Object
    {
        if (values == null || index < 0 || index >= values.Length)
            return null;

        return values[index];
    }

    private static T GetLiveOrLoad<T>(T current, string path) where T : Object
    {
        if (current != null)
            return current;

        T loaded = AssetDatabase.LoadAssetAtPath<T>(path);
        if (loaded == null)
            Debug.LogError("CharacterSelectionSceneBuilder: could not load generated asset: " + path);

        return loaded;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static TMP_Text[] CreateRadarLabels(Transform parent)
    {
        string[] names = { "Vitality", "Attack", "Defense", "Mobility", "Range", "Prediction" };
        Vector2[] positions =
        {
            new Vector2(0f, 112f),
            new Vector2(118f, 58f),
            new Vector2(118f, -58f),
            new Vector2(0f, -112f),
            new Vector2(-118f, -58f),
            new Vector2(-118f, 58f)
        };

        TMP_Text[] labels = new TMP_Text[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            labels[i] = AddText(parent, names[i] + "Label", string.Empty, 10, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), positions[i], new Vector2(82f, 20f));
            labels[i].gameObject.SetActive(false);
        }

        return labels;
    }

    private static GameObject CreatePanel(string name, Vector2 size, Color color)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        Image image = root.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return root;
    }

    private static GameObject CreateAnchoredPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        GameObject root = CreatePanel(name, sizeDelta, color);
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return root;
    }

    private static GameObject AddFullImage(Transform parent, string name, Color color)
    {
        GameObject obj = CreateAnchoredPanel(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return obj;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject root = CreateAnchoredPanel(parent, name, anchor, anchor, position, size, new Color(0.12f, 0.16f, 0.22f, 0.95f));
        Image image = root.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        AddText(root.transform, "Text", label, 16, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        return button;
    }

    private static Toggle CreateToggle(Transform parent, string name, string label, Vector2 anchor, Vector2 position)
    {
        GameObject root = CreateAnchoredPanel(parent, name, anchor, anchor, position, new Vector2(176f, 42f), new Color(0.08f, 0.10f, 0.14f, 0.92f));
        root.GetComponent<Image>().raycastTarget = true;
        Toggle toggle = root.AddComponent<Toggle>();
        Image background = AddImage(root.transform, "Background", new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(24f, 24f), null, new Color(0.22f, 0.24f, 0.32f, 1f));
        Image checkmark = AddImage(background.transform, "Checkmark", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(15f, 15f), null, new Color(0.95f, 0.75f, 0.28f, 1f));
        AddText(root.transform, "Label", label, 15, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(110f, 28f));
        toggle.targetGraphic = background;
        toggle.graphic = null;
        toggle.transition = Selectable.Transition.None;
        checkmark.enabled = false;
        return toggle;
    }

    private static Image AddImage(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Sprite sprite, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = obj.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text AddText(Transform parent, string name, string text, int size, TextAlignmentOptions alignment, Vector2 anchor, Vector2 position, Vector2 rectSize)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = rectSize;
        TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    private static GameObject AddBadge(Transform parent, string text, Vector2 anchor, Vector2 position, Color color)
    {
        GameObject badge = CreateAnchoredPanel(parent, text + "Marker", anchor, anchor, position, new Vector2(82f, 28f), color);
        AddText(badge.transform, "Text", text, 12, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 24f));
        return badge;
    }

    private static GameObject AddOverlay(Transform parent, string name, Color color, string label)
    {
        GameObject overlay = CreateAnchoredPanel(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color);
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        if (!string.IsNullOrEmpty(label))
            AddText(overlay.transform, "Text", label, 22, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 40f));
        return overlay;
    }

    private static RuntimeAnimatorController LoadGeneratedUIController()
    {
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(UIControllerPath);
        if (controller == null)
            Debug.LogWarning("CharacterSelectionSceneBuilder: generated UI animator controller is missing: " + UIControllerPath);

        return controller;
    }

    private static Animator AddAnimator(GameObject root, RuntimeAnimatorController controller)
    {
        Animator animator = root.GetComponent<Animator>();
        if (animator == null)
            animator = root.AddComponent<Animator>();

        animator.runtimeAnimatorController = controller;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        return animator;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject root)
    {
        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = root.AddComponent<CanvasGroup>();

        return canvasGroup;
    }

    private static CharacterSelectionAnimationController AddAnimationController(GameObject root, RuntimeAnimatorController controller)
    {
        Animator animator = AddAnimator(root, controller);
        CanvasGroup canvasGroup = EnsureCanvasGroup(root);
        CharacterSelectionAnimationController animation = root.GetComponent<CharacterSelectionAnimationController>();
        if (animation == null)
            animation = root.AddComponent<CharacterSelectionAnimationController>();

        SetRef(animation, "animator", animator);
        SetRef(animation, "canvasGroup", canvasGroup);
        return animation;
    }

    private static RectTransform FindChildRect(Transform root, string childName)
    {
        Transform child = root.Find(childName);
        return child as RectTransform;
    }

    private static void SavePrefab(GameObject root, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            AssetDatabase.DeleteAsset(path);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    private static void SetRef(Object target, string fieldName, Object value)
    {
        if (target == null)
            return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property != null)
            property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetString(Object target, string fieldName, string value)
    {
        if (target == null)
            return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property != null)
            property.stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetBool(Object target, string fieldName, bool value)
    {
        if (target == null)
            return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property != null)
            property.boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetFloat(Object target, string fieldName, float value)
    {
        if (target == null)
            return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property != null)
            property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetArray(Object target, string fieldName, Object[] values)
    {
        if (target == null)
            return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property != null && property.isArray)
        {
            property.arraySize = values != null ? values.Length : 0;
            for (int i = 0; values != null && i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == scenePath)
            {
                scenes[i].enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
