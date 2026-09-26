using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OracleSkillUIPrefabBuilder
{
    private const int IconSize = 96;
    private const string OracleConfigPath = "Assets/Scripts/game/classes/OracleConfig.asset";
    private const string SourcePrefabFolder = "Assets/Resources/Prefab/Oracle/UI/Actions";
    private const string SpriteFolder = "Assets/Resources/Prefab/Oracle/UI/Sprites";
    private const string OracleGameplayUiPath = "Assets/Resources/Prefab/Oracle/UI/OracleGameplayUI.prefab";
    private const string KnightGameplayUiPath = "Assets/Resources/Prefab/knight/knightUiPrefab.prefab";
    private const string SharedDancePrefabPath = "Assets/Resources/Prefab/Dance.prefab";

    private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);
    private static readonly Color32 DeepBlue = new Color32(13, 22, 46, 255);
    private static readonly Color32 DarkViolet = new Color32(45, 19, 72, 255);
    private static readonly Color32 Cyan = new Color32(62, 225, 255, 255);
    private static readonly Color32 PaleViolet = new Color32(181, 157, 255, 255);
    private static readonly Color32 Gold = new Color32(239, 195, 93, 255);
    private static readonly Color32 PaleBlue = new Color32(143, 225, 255, 255);
    private static readonly Color32 Magenta = new Color32(237, 67, 255, 255);
    private static readonly Color32 SilverViolet = new Color32(196, 202, 232, 255);
    private static readonly Color32 GrayViolet = new Color32(136, 122, 168, 210);

    [MenuItem("Tools/Oracle/Build Skill UI")]
    public static void BuildFromMenu()
    {
        Build();
    }

    public static void BuildFromCommandLine()
    {
        Build();
        EditorApplication.Exit(0);
    }

    private static void Build()
    {
        EnsureFolder(SourcePrefabFolder);
        EnsureFolder(SpriteFolder);

        Dictionary<string, Sprite> sprites = GenerateSprites();
        OracleActionSpec[] specs = CreateSpecs(sprites);
        OracleActionSpec[] layoutSpecs = CreateLayoutSpecs(sprites);
        Dictionary<ActionType, GameObject> actionPrefabs = new Dictionary<ActionType, GameObject>();

        for (int i = 0; i < specs.Length; i++)
        {
            GameObject prefab = CreateActionButtonPrefab(specs[i], sprites);
            actionPrefabs[specs[i].actionType] = prefab;
        }

        actionPrefabs[ActionType.Dance] = AssetDatabase.LoadAssetAtPath<GameObject>(SharedDancePrefabPath);

        GameObject gameplayUiPrefab = BuildOracleGameplayUi(layoutSpecs, actionPrefabs, sprites);
        AssignOracleConfig(specs, actionPrefabs, sprites, gameplayUiPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Oracle skill UI prefabs generated.");
    }

    private static OracleActionSpec[] CreateSpecs(Dictionary<string, Sprite> sprites)
    {
        return new OracleActionSpec[]
        {
            new OracleActionSpec(ActionType.Bolt, "Bolt", 1, "2-4", "A ranged projectile that hits jumping targets. Can be blocked or parried.", sprites["Bolt"]),
            new OracleActionSpec(ActionType.Rift, "Rift", 2, "2-3", "A charged rift that ignores Parry and partially pierces Defense. Jump avoids it.", sprites["Rift"]),
            new OracleActionSpec(ActionType.Shift, "Shift", 2, "Swap", "Vanish and shift behind the enemy after the swap point. Costs 2 slots, once per turn, and close Light Attack interrupts it.", sprites["Shift"]),
            new OracleActionSpec(ActionType.Ward, "Ward", 1, string.Empty, "Blocks Light Attack. Heavy Attack breaks through, and Low Attack bypasses it.", sprites["Ward"]),
            new OracleActionSpec(ActionType.Fade, "Fade", 2, "Max 4", "Move backward 2 cells, up to distance 4. Interrupted by close-range Light Attack.", sprites["Fade"]),
            new OracleActionSpec(ActionType.Sight, "Sight", 1, "Full", "Once per match. Starting next round, permanently reveal one fixed enemy action slot after the enemy is ready.", sprites["Sight"])
        };
    }

    private static OracleActionSpec[] CreateLayoutSpecs(Dictionary<string, Sprite> sprites)
    {
        return new OracleActionSpec[]
        {
            new OracleActionSpec(ActionType.Bolt, "Bolt", 1, "2-4", "A ranged projectile that hits jumping targets. Can be blocked or parried.", sprites["Bolt"]),
            new OracleActionSpec(ActionType.Rift, "Rift", 2, "2-3", "A charged rift that ignores Parry and partially pierces Defense. Jump avoids it.", sprites["Rift"]),
            new OracleActionSpec(ActionType.Shift, "Shift", 2, "Swap", "Vanish and shift behind the enemy after the swap point. Costs 2 slots, once per turn, and close Light Attack interrupts it.", sprites["Shift"]),
            new OracleActionSpec(ActionType.Ward, "Ward", 1, string.Empty, "Blocks Light Attack. Heavy Attack breaks through, and Low Attack bypasses it.", sprites["Ward"]),
            new OracleActionSpec(ActionType.Fade, "Fade", 2, "Max 4", "Move backward 2 cells, up to distance 4. Interrupted by close-range Light Attack.", sprites["Fade"]),
            new OracleActionSpec(ActionType.Sight, "Sight", 1, "Full", "Once per match. Starting next round, permanently reveal one fixed enemy action slot after the enemy is ready.", sprites["Sight"]),
            new OracleActionSpec(ActionType.Dance, "Dance", 1, string.Empty, "Gain 1 energy.", sprites["Dance"]),
            new OracleActionSpec(ActionType.MoveForward, "Move Forward", 1, string.Empty, "Step toward the enemy.", sprites["MoveForward"]),
            new OracleActionSpec(ActionType.MoveBackward, "Move Backward", 1, string.Empty, "Step away from the enemy.", sprites["MoveBackward"]),
            new OracleActionSpec(ActionType.Jump, "Jump", 1, string.Empty, "Jump to interact with incoming attacks.", sprites["Jump"])
        };
    }

    private static Dictionary<string, Sprite> GenerateSprites()
    {
        Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        WriteSprite("Oracle_ButtonBase", DrawButtonBase());
        WriteSprite("Oracle_DisabledOverlay", DrawDisabledOverlay());
        WriteSprite("Oracle_CooldownOverlay", DrawCooldownOverlay());
        WriteSprite("Oracle_UsedOverlay", DrawUsedOverlay());
        WriteSprite("Oracle_RiftContinuation", DrawRiftContinuation());
        WriteSprite("Oracle_SightFrame", DrawSightFrame());
        WriteSprite("Bolt", DrawBoltIcon());
        WriteSprite("Rift", DrawRiftIcon());
        WriteSprite("Shift", DrawShiftIcon());
        WriteSprite("Ward", DrawWardIcon());
        WriteSprite("Fade", DrawFadeIcon());
        WriteSprite("Sight", DrawSightIcon());
        WriteSprite("Dance", DrawDanceIcon());
        WriteSprite("MoveForward", DrawMoveForwardIcon());
        WriteSprite("MoveBackward", DrawMoveBackwardIcon());
        WriteSprite("Jump", DrawJumpIcon());

        AssetDatabase.Refresh();

        string[] keys =
        {
            "Oracle_ButtonBase",
            "Oracle_DisabledOverlay",
            "Oracle_CooldownOverlay",
            "Oracle_UsedOverlay",
            "Oracle_RiftContinuation",
            "Oracle_SightFrame",
            "Bolt",
            "Rift",
            "Shift",
            "Ward",
            "Fade",
            "Sight",
            "Dance",
            "MoveForward",
            "MoveBackward",
            "Jump"
        };

        for (int i = 0; i < keys.Length; i++)
        {
            string path = GetSpritePath(keys[i]);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            sprites[keys[i]] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        return sprites;
    }

    private static GameObject CreateActionButtonPrefab(OracleActionSpec spec, Dictionary<string, Sprite> sprites)
    {
        GameObject root = CreateButtonObject(spec, sprites, true);
        string path = SourcePrefabFolder + "/Oracle_" + spec.name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildOracleGameplayUi(
        OracleActionSpec[] specs,
        Dictionary<ActionType, GameObject> actionPrefabs,
        Dictionary<string, Sprite> sprites)
    {
        if (!File.Exists(OracleGameplayUiPath) && File.Exists(KnightGameplayUiPath))
            AssetDatabase.CopyAsset(KnightGameplayUiPath, OracleGameplayUiPath);

        GameObject root = PrefabUtility.LoadPrefabContents(OracleGameplayUiPath);
        try
        {
            root.name = "OracleGameplayUI";
            ActionDragSource[] sources = root.GetComponentsInChildren<ActionDragSource>(true);
            Array.Sort(sources, (a, b) => GetHierarchyIndexPath(a.transform).CompareTo(GetHierarchyIndexPath(b.transform)));

            int count = Mathf.Min(specs.Length, sources.Length);
            for (int i = 0; i < count; i++)
            {
                GameObject actionPrefab = null;
                actionPrefabs.TryGetValue(specs[i].actionType, out actionPrefab);
                ApplySpecToExistingSource(sources[i], specs[i], actionPrefab, sprites);
            }

            for (int i = count; i < sources.Length; i++)
                sources[i].gameObject.SetActive(false);

            ClassGameplayUILayout layout = root.GetComponent<ClassGameplayUILayout>();
            if (layout == null)
                layout = root.GetComponentInChildren<ClassGameplayUILayout>(true);

            if (layout != null)
                AssignSkillSources(layout, sources, count);

            PrefabUtility.SaveAsPrefabAsset(root, OracleGameplayUiPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(OracleGameplayUiPath);
    }

    private static void ApplySpecToExistingSource(
        ActionDragSource source,
        OracleActionSpec spec,
        GameObject actionPrefab,
        Dictionary<string, Sprite> sprites)
    {
        if (source == null)
            return;

        GameObject root = source.gameObject;
        root.name = "Oracle_" + spec.name;
        root.SetActive(true);

        RectTransform rect = root.GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = new Vector2(IconSize, IconSize);

        Image rootImage = root.GetComponent<Image>();
        if (rootImage == null)
            rootImage = root.AddComponent<Image>();

        rootImage.sprite = sprites["Oracle_ButtonBase"];
        rootImage.color = Color.white;
        rootImage.raycastTarget = true;

        ActionDragData dragData = root.GetComponent<ActionDragData>();
        if (dragData == null)
            dragData = root.AddComponent<ActionDragData>();

        dragData.actionType = spec.actionType;

        if (root.GetComponent<CanvasGroup>() == null)
            root.AddComponent<CanvasGroup>();

        if (root.GetComponent<DraggableItem>() == null)
            root.AddComponent<DraggableItem>();

        ActionButtonView view = root.GetComponent<ActionButtonView>();
        if (view == null)
            view = root.AddComponent<ActionButtonView>();

        RemoveGeneratedChildren(root.transform);
        ButtonVisualRefs refs = AddButtonVisuals(root.transform, spec, sprites);
        AssignButtonView(view, refs);
        AssignDragSource(source, spec, actionPrefab, sprites);
    }

    private static GameObject CreateButtonObject(OracleActionSpec spec, Dictionary<string, Sprite> sprites, bool addDragSource)
    {
        GameObject root = new GameObject("Oracle_" + spec.name, typeof(RectTransform));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(IconSize, IconSize);

        Image rootImage = root.AddComponent<Image>();
        rootImage.sprite = sprites["Oracle_ButtonBase"];
        rootImage.color = Color.white;
        rootImage.raycastTarget = true;

        root.AddComponent<CanvasGroup>();

        ActionDragData dragData = root.AddComponent<ActionDragData>();
        dragData.actionType = spec.actionType;

        root.AddComponent<DraggableItem>();

        ActionDragSource source = null;
        if (addDragSource)
            source = root.AddComponent<ActionDragSource>();

        ActionButtonView view = root.AddComponent<ActionButtonView>();
        ButtonVisualRefs refs = AddButtonVisuals(root.transform, spec, sprites);
        AssignButtonView(view, refs);

        if (source != null)
            AssignDragSource(source, spec, null, sprites);

        return root;
    }

    private static ButtonVisualRefs AddButtonVisuals(Transform parent, OracleActionSpec spec, Dictionary<string, Sprite> sprites)
    {
        ButtonVisualRefs refs = new ButtonVisualRefs();
        refs.icon = AddImage(parent, "Icon", spec.icon, new Vector2(0.5f, 0.5f), new Vector2(66f, 66f), Color.white);
        refs.costText = AddBadge(parent, "CostBadge", new Vector2(0f, 1f), new Vector2(23f, -23f), spec.cost.ToString(), Gold);
        refs.rangeText = AddBadge(parent, "RangeBadge", new Vector2(1f, 0f), new Vector2(-28f, 22f), spec.rangeText, Cyan);
        refs.disabledOverlay = AddImage(parent, "DisabledOverlay", sprites["Oracle_DisabledOverlay"], new Vector2(0.5f, 0.5f), new Vector2(96f, 96f), Color.white);
        refs.cooldownOverlay = AddImage(parent, "CooldownOverlay", sprites["Oracle_CooldownOverlay"], new Vector2(0.5f, 0.5f), new Vector2(96f, 96f), Color.white);
        refs.usedOverlay = AddImage(parent, "UsedOverlay", sprites["Oracle_UsedOverlay"], new Vector2(0.5f, 0.5f), new Vector2(96f, 96f), Color.white);
        refs.energyRing = AddImage(parent, "EnergyRing", sprites["Oracle_SightFrame"], new Vector2(0.5f, 0.5f), new Vector2(94f, 94f), spec.actionType == ActionType.Sight ? Color.white : new Color(1f, 1f, 1f, 0f));
        refs.tooltipTitle = AddTooltipText(parent, "TooltipTitle", spec.name, 0);
        refs.tooltipDescription = AddTooltipText(parent, "TooltipDescription", spec.tooltipDescription, 1);

        refs.disabledOverlay.gameObject.SetActive(false);
        refs.cooldownOverlay.gameObject.SetActive(false);
        refs.usedOverlay.gameObject.SetActive(false);
        refs.tooltipTitle.transform.parent.gameObject.SetActive(false);

        return refs;
    }

    private static Image AddImage(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 size, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image image = obj.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private static Text AddBadge(Transform parent, string name, Vector2 anchor, Vector2 position, string text, Color accentColor)
    {
        GameObject badge = new GameObject(name, typeof(RectTransform));
        badge.transform.SetParent(parent, false);

        RectTransform rect = badge.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(33f, 22f);

        Image image = badge.AddComponent<Image>();
        image.color = new Color(0.02f, 0.03f, 0.07f, 0.82f);
        image.raycastTarget = false;

        Text label = AddText(badge.transform, name.Replace("Badge", "Text"), text, 14, accentColor);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        return label;
    }

    private static Text AddTooltipText(Transform parent, string name, string text, int line)
    {
        Transform root = parent.Find("TooltipRoot");
        if (root == null)
        {
            GameObject tooltip = new GameObject("TooltipRoot", typeof(RectTransform));
            tooltip.transform.SetParent(parent, false);
            RectTransform tooltipRect = tooltip.GetComponent<RectTransform>();
            tooltipRect.anchorMin = new Vector2(1f, 1f);
            tooltipRect.anchorMax = new Vector2(1f, 1f);
            tooltipRect.pivot = new Vector2(0f, 1f);
            tooltipRect.anchoredPosition = new Vector2(8f, 0f);
            tooltipRect.sizeDelta = new Vector2(260f, 84f);
            root = tooltip.transform;
        }

        Text label = AddText(root, name, text, line == 0 ? 16 : 12, line == 0 ? Gold : Color.white);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(0f, line == 0 ? 0f : -24f);
        rect.sizeDelta = new Vector2(260f, line == 0 ? 24f : 60f);
        label.alignment = TextAnchor.UpperLeft;
        return label;
    }

    private static Text AddText(Transform parent, string name, string text, int size, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        Text label = obj.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = size;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static void AssignButtonView(ActionButtonView view, ButtonVisualRefs refs)
    {
        SerializedObject serialized = new SerializedObject(view);
        SetReference(serialized, "iconImage", refs.icon);
        SetReference(serialized, "costText", refs.costText);
        SetReference(serialized, "rangeText", refs.rangeText);
        SetReference(serialized, "disabledOverlay", refs.disabledOverlay);
        SetReference(serialized, "cooldownOverlay", refs.cooldownOverlay);
        SetReference(serialized, "usedOverlay", refs.usedOverlay);
        SetReference(serialized, "tooltipTitleText", refs.tooltipTitle);
        SetReference(serialized, "tooltipDescriptionText", refs.tooltipDescription);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignDragSource(
        ActionDragSource source,
        OracleActionSpec spec,
        GameObject prefab,
        Dictionary<string, Sprite> sprites)
    {
        SerializedObject serialized = new SerializedObject(source);
        SetReference(serialized, "dragPrefab", prefab);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        ActionData displayData = new ActionData
        {
            actionName = spec.name,
            actionType = spec.actionType,
            sourcePrefab = prefab,
            slotCost = spec.cost,
            iconSprite = spec.icon,
            disabledOverlaySprite = sprites["Oracle_DisabledOverlay"],
            cooldownOverlaySprite = spec.actionType == ActionType.Fade || spec.actionType == ActionType.Shift ? sprites["Oracle_CooldownOverlay"] : null,
            usedOverlaySprite = spec.actionType == ActionType.Sight ? sprites["Oracle_UsedOverlay"] : null,
            lockedContinuationSprite = spec.actionType == ActionType.Rift ? sprites["Oracle_RiftContinuation"] : spec.icon,
            tooltipTitle = spec.name,
            tooltipDescription = spec.tooltipDescription,
            rangeText = spec.rangeText
        };

        SetPrivateField(source, "actionData", displayData);
        SetPrivateField(source, "actionType", spec.actionType);
        EditorUtility.SetDirty(source);
    }

    private static void AssignSkillSources(ClassGameplayUILayout layout, ActionDragSource[] sources, int count)
    {
        SerializedObject serialized = new SerializedObject(layout);
        SerializedProperty property = serialized.FindProperty("skillSources");
        if (property != null && property.isArray)
        {
            property.arraySize = count;
            for (int i = 0; i < count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = sources[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AssignOracleConfig(
        OracleActionSpec[] specs,
        Dictionary<ActionType, GameObject> actionPrefabs,
        Dictionary<string, Sprite> sprites,
        GameObject gameplayUiPrefab)
    {
        OracleClassConfig config = AssetDatabase.LoadAssetAtPath<OracleClassConfig>(OracleConfigPath);
        if (config == null)
        {
            Debug.LogError("OracleConfig.asset is missing.");
            return;
        }

        config.className = "Oracle";
        config.photonResourceFolder = "Oracle";
        config.maxHP = 25;
        config.slotCount = 5;
        config.skinNames = new[] { "Oracle_0" };
        config.skinPrefabs = new[] { AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefab/Oracle/Oracle_0.prefab") };
        config.gameplayUiPrefab = gameplayUiPrefab;
        config.sightSelectedSlotFrame = sprites["Oracle_SightFrame"];
        config.sightUsedOverlay = sprites["Oracle_UsedOverlay"];
        config.shiftCooldownOverlay = sprites["Oracle_CooldownOverlay"];
        config.riftSecondarySlotLockedIcon = sprites["Oracle_RiftContinuation"];
        ClearInheritedCombatActions(config);
        AssignDanceAction(config, sprites);

        for (int i = 0; i < specs.Length; i++)
            AssignActionData(config, specs[i], actionPrefabs[specs[i].actionType], sprites);

        EditorUtility.SetDirty(config);
    }

    private static void ClearInheritedCombatActions(OracleClassConfig config)
    {
        if (config == null)
            return;

        ClearAttackAction(config.lightAttack);
        ClearAttackAction(config.heavyAttack);
        ClearAttackAction(config.lowAttack);
        ClearAction(config.parry);
        ClearAction(config.defense);
        ClearUltimateAction(config.ultimate);
    }

    private static void AssignDanceAction(OracleClassConfig config, Dictionary<string, Sprite> sprites)
    {
        if (config == null)
            return;

        if (config.dance == null)
            config.dance = new DanceActionData();

        ActionData action = config.dance;
        action.actionName = "Dance";
        action.actionType = ActionType.Dance;
        action.sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharedDancePrefabPath);
        action.slotCost = 1;
        action.iconSprite = sprites.ContainsKey("Dance") ? sprites["Dance"] : null;
        action.disabledOverlaySprite = sprites.ContainsKey("Oracle_DisabledOverlay") ? sprites["Oracle_DisabledOverlay"] : null;
        action.cooldownOverlaySprite = null;
        action.usedOverlaySprite = null;
        action.lockedContinuationSprite = action.iconSprite;
        action.tooltipTitle = "Dance";
        action.tooltipDescription = "Gain 1 energy.";
        action.rangeText = string.Empty;
        config.dance.energyGain = 1;
    }

    private static void AssignActionData(
        OracleClassConfig config,
        OracleActionSpec spec,
        GameObject sourcePrefab,
        Dictionary<string, Sprite> sprites)
    {
        ActionData action = config.GetActionData(spec.actionType);
        if (action == null)
            return;

        action.actionName = spec.name;
        action.actionType = spec.actionType;
        action.slotCost = spec.cost;
        action.sourcePrefab = sourcePrefab;
        action.iconSprite = spec.icon;
        action.disabledOverlaySprite = sprites["Oracle_DisabledOverlay"];
        action.cooldownOverlaySprite = spec.actionType == ActionType.Fade || spec.actionType == ActionType.Shift ? sprites["Oracle_CooldownOverlay"] : null;
        action.usedOverlaySprite = spec.actionType == ActionType.Sight ? sprites["Oracle_UsedOverlay"] : null;
        action.lockedContinuationSprite = spec.actionType == ActionType.Rift ? sprites["Oracle_RiftContinuation"] : spec.icon;
        action.tooltipTitle = spec.name;
        action.tooltipDescription = spec.tooltipDescription;
        action.rangeText = spec.rangeText;

        if (action is AttackActionData attack)
        {
            if (spec.actionType == ActionType.Bolt)
            {
                attack.damage = 3;
                attack.minRange = 2;
                attack.range = 4;
                attack.canBeParried = true;
                attack.canBeDefended = true;
                attack.jumpInteraction = JumpInteractionType.None;
                attack.requiresCharge = false;
                attack.chargeTurns = 0;
            }
            else if (spec.actionType == ActionType.Rift)
            {
                attack.damage = 4;
                attack.minRange = 2;
                attack.range = 3;
                attack.canBeParried = false;
                attack.canBeDefended = true;
                attack.jumpInteraction = JumpInteractionType.Evade;
                attack.requiresCharge = true;
                attack.chargeTurns = 1;
            }
        }

        if (action is UltimateActionData ultimate)
        {
            ultimate.damage = 0;
            ultimate.minRange = 0;
            ultimate.range = 0;
            ultimate.canBeParried = false;
            ultimate.canBeDefended = false;
            ultimate.jumpInteraction = JumpInteractionType.None;
            ultimate.requiresCharge = false;
            ultimate.chargeTurns = 0;
        }
    }

    private static void ClearAction(ActionData action)
    {
        if (action == null)
            return;

        action.actionName = string.Empty;
        action.actionType = ActionType.None;
        action.sourcePrefab = null;
        action.slotCost = 1;
        action.iconSprite = null;
        action.disabledOverlaySprite = null;
        action.cooldownOverlaySprite = null;
        action.usedOverlaySprite = null;
        action.lockedContinuationSprite = null;
        action.tooltipTitle = string.Empty;
        action.tooltipDescription = string.Empty;
        action.rangeText = string.Empty;
    }

    private static void ClearAttackAction(AttackActionData action)
    {
        if (action == null)
            return;

        ClearAction(action);
        action.damage = 1;
        action.minRange = 0;
        action.range = 1;
        action.canBeParried = false;
        action.canBeDefended = true;
        action.jumpInteraction = JumpInteractionType.None;
        action.requiresCharge = false;
        action.chargeTurns = 1;
    }

    private static void ClearUltimateAction(UltimateActionData action)
    {
        if (action == null)
            return;

        ClearAction(action);
        action.damage = 0;
        action.minRange = 0;
        action.range = 0;
        action.canBeParried = false;
        action.canBeDefended = true;
        action.jumpInteraction = JumpInteractionType.None;
        action.requiresCharge = false;
        action.chargeTurns = 1;
    }

    private static void SetReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
            field.SetValue(target, value);
    }

    private static void RemoveGeneratedChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    private static string GetHierarchyIndexPath(Transform transform)
    {
        string path = transform.GetSiblingIndex().ToString("D4");
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.GetSiblingIndex().ToString("D4") + "/" + path;
        }

        return path;
    }

    private static void WriteSprite(string name, Texture2D texture)
    {
        string path = GetSpritePath(name);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static string GetSpritePath(string name)
    {
        return SpriteFolder + "/" + name + ".png";
    }

    private static Texture2D NewTexture()
    {
        Texture2D texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[IconSize * IconSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Transparent;

        texture.SetPixels32(pixels);
        return texture;
    }

    private static Texture2D DrawButtonBase()
    {
        Texture2D texture = NewTexture();
        FillRect(texture, 8, 8, 80, 80, DeepBlue);
        FillRect(texture, 12, 12, 72, 72, new Color32(23, 18, 47, 255));
        DrawRect(texture, 8, 8, 80, 80, Gold);
        DrawRect(texture, 13, 13, 70, 70, PaleViolet);
        DrawEye(texture, 48, 48, 34, 12, new Color32(43, 83, 126, 90), Gold);
        DrawLine(texture, 18, 78, 78, 18, new Color32(75, 206, 230, 90), 1);
        DrawLine(texture, 18, 18, 78, 78, new Color32(142, 98, 255, 70), 1);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawDisabledOverlay()
    {
        Texture2D texture = NewTexture();
        FillRect(texture, 0, 0, 96, 96, new Color32(0, 0, 0, 165));
        DrawLine(texture, 26, 70, 70, 26, new Color32(168, 186, 214, 180), 4);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawCooldownOverlay()
    {
        Texture2D texture = NewTexture();
        FillRect(texture, 0, 0, 96, 96, new Color32(6, 8, 20, 125));
        DrawLine(texture, 28, 30, 68, 66, Cyan, 3);
        DrawLine(texture, 68, 30, 28, 66, PaleViolet, 3);
        DrawCircle(texture, 48, 48, 28, Gold);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawUsedOverlay()
    {
        Texture2D texture = NewTexture();
        FillRect(texture, 0, 0, 96, 96, new Color32(4, 5, 13, 175));
        DrawEye(texture, 48, 50, 46, 18, new Color32(94, 36, 105, 230), Gold);
        DrawLine(texture, 39, 66, 46, 48, new Color32(255, 225, 145, 240), 2);
        DrawLine(texture, 51, 50, 59, 30, new Color32(255, 225, 145, 240), 2);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawRiftContinuation()
    {
        Texture2D texture = NewTexture();
        DrawLine(texture, 12, 46, 34, 52, new Color32(194, 45, 255, 150), 4);
        DrawLine(texture, 34, 52, 58, 40, new Color32(194, 45, 255, 150), 4);
        DrawLine(texture, 58, 40, 84, 48, new Color32(194, 45, 255, 150), 4);
        DrawCircle(texture, 48, 48, 30, new Color32(135, 76, 255, 120));
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawSightFrame()
    {
        Texture2D texture = NewTexture();
        DrawCircle(texture, 48, 48, 42, Gold);
        DrawCircle(texture, 48, 48, 36, new Color32(107, 76, 255, 210));
        DrawEye(texture, 48, 48, 48, 18, Transparent, Gold);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawBoltIcon()
    {
        Texture2D texture = NewTexture();
        DrawLine(texture, 18, 26, 68, 72, Cyan, 5);
        DrawLine(texture, 14, 18, 54, 54, new Color32(164, 246, 255, 130), 2);
        DrawStar(texture, 70, 74, 14, 6, new Color32(230, 253, 255, 255));
        DrawStar(texture, 70, 74, 20, 4, new Color32(112, 132, 255, 130));
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawRiftIcon()
    {
        Texture2D texture = NewTexture();
        FillCircle(texture, 48, 46, 16, new Color32(5, 2, 15, 245));
        DrawCircle(texture, 48, 46, 24, Magenta);
        DrawLine(texture, 16, 48, 34, 54, Magenta, 4);
        DrawLine(texture, 34, 54, 47, 40, PaleViolet, 4);
        DrawLine(texture, 47, 40, 63, 58, Magenta, 4);
        DrawLine(texture, 63, 58, 82, 48, Magenta, 4);
        DrawEye(texture, 48, 46, 34, 11, Transparent, new Color32(245, 92, 255, 220));
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawShiftIcon()
    {
        Texture2D texture = NewTexture();
        DrawSilhouette(texture, 32, 48, Cyan);
        DrawSilhouette(texture, 64, 48, PaleViolet);
        DrawArc(texture, 48, 48, 26, 30, 150, new Color32(86, 210, 255, 255), 3);
        DrawArc(texture, 48, 48, 26, 210, 330, new Color32(178, 108, 255, 255), 3);
        DrawLine(texture, 25, 61, 18, 66, Cyan, 3);
        DrawLine(texture, 71, 35, 78, 30, PaleViolet, 3);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawWardIcon()
    {
        Texture2D texture = NewTexture();
        FillCircle(texture, 48, 48, 31, new Color32(88, 175, 226, 95));
        FillCircle(texture, 58, 48, 30, Transparent);
        DrawCircle(texture, 48, 48, 31, PaleBlue);
        DrawEye(texture, 44, 50, 31, 12, Transparent, SilverViolet);
        FillCircle(texture, 44, 50, 4, Gold);
        DrawLine(texture, 68, 25, 77, 34, PaleBlue, 3);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawFadeIcon()
    {
        Texture2D texture = NewTexture();
        DrawSilhouette(texture, 58, 48, new Color32(210, 222, 255, 235));
        DrawSilhouette(texture, 45, 48, GrayViolet);
        DrawSilhouette(texture, 34, 48, new Color32(116, 92, 153, 120));
        DrawLine(texture, 27, 30, 71, 67, new Color32(176, 145, 255, 100), 2);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawSightIcon()
    {
        Texture2D texture = NewTexture();
        DrawEye(texture, 48, 58, 56, 22, new Color32(63, 35, 98, 210), Gold);
        FillCircle(texture, 48, 58, 9, new Color32(29, 12, 55, 255));
        FillCircle(texture, 48, 58, 4, Cyan);
        for (int i = 0; i < 5; i++)
        {
            int x = 24 + i * 12;
            DrawRect(texture, x, 24, 8, 8, i == 2 ? Gold : PaleViolet);
        }
        DrawLine(texture, 48, 50, 52, 32, new Color32(255, 225, 120, 180), 2);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawMoveForwardIcon()
    {
        Texture2D texture = NewTexture();
        DrawLine(texture, 24, 48, 68, 48, Cyan, 5);
        DrawLine(texture, 68, 48, 54, 34, Cyan, 5);
        DrawLine(texture, 68, 48, 54, 62, Cyan, 5);
        DrawLine(texture, 20, 64, 72, 28, new Color32(168, 108, 255, 120), 2);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawMoveBackwardIcon()
    {
        Texture2D texture = NewTexture();
        DrawLine(texture, 72, 48, 28, 48, PaleViolet, 5);
        DrawLine(texture, 28, 48, 42, 34, PaleViolet, 5);
        DrawLine(texture, 28, 48, 42, 62, PaleViolet, 5);
        DrawLine(texture, 24, 28, 76, 64, new Color32(84, 225, 255, 120), 2);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawJumpIcon()
    {
        Texture2D texture = NewTexture();
        DrawSilhouette(texture, 48, 39, new Color32(220, 234, 255, 230));
        DrawArc(texture, 48, 47, 30, 205, 335, Cyan, 3, 22);
        DrawLine(texture, 68, 37, 77, 41, Cyan, 3);
        DrawLine(texture, 68, 37, 66, 48, Cyan, 3);
        DrawStar(texture, 28, 65, 8, 4, Gold);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawDanceIcon()
    {
        Texture2D texture = NewTexture();
        DrawSilhouette(texture, 46, 44, new Color32(232, 220, 255, 235));
        DrawArc(texture, 48, 46, 30, 28, 260, new Color32(178, 108, 255, 230), 3);
        DrawLine(texture, 27, 59, 18, 55, new Color32(178, 108, 255, 230), 3);
        DrawLine(texture, 27, 59, 24, 68, new Color32(178, 108, 255, 230), 3);
        DrawStar(texture, 66, 68, 9, 4, Gold);
        DrawStar(texture, 74, 45, 6, 3, Cyan);
        texture.Apply();
        return texture;
    }

    private static void DrawSilhouette(Texture2D texture, int cx, int cy, Color32 color)
    {
        FillCircle(texture, cx, cy + 14, 7, color);
        FillRect(texture, cx - 6, cy - 9, 12, 22, color);
        DrawLine(texture, cx - 10, cy + 4, cx + 10, cy + 4, color, 3);
    }

    private static void DrawEye(Texture2D texture, int cx, int cy, int width, int height, Color32 fill, Color32 line)
    {
        if (fill.a > 0)
            FillEllipse(texture, cx, cy, width / 2, height / 2, fill);

        DrawArc(texture, cx, cy, width / 2, 0, 180, line, 2, height / 2);
        DrawArc(texture, cx, cy, width / 2, 180, 360, line, 2, height / 2);
    }

    private static void DrawStar(Texture2D texture, int cx, int cy, int radius, int innerRadius, Color32 color)
    {
        Vector2 previous = Vector2.zero;
        for (int i = 0; i <= 10; i++)
        {
            float angle = Mathf.Deg2Rad * (-90f + i * 36f);
            float r = i % 2 == 0 ? radius : innerRadius;
            Vector2 point = new Vector2(cx + Mathf.Cos(angle) * r, cy + Mathf.Sin(angle) * r);
            if (i > 0)
                DrawLine(texture, Mathf.RoundToInt(previous.x), Mathf.RoundToInt(previous.y), Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), color, 2);

            previous = point;
        }
    }

    private static void DrawArc(Texture2D texture, int cx, int cy, int radius, float startDegrees, float endDegrees, Color32 color, int thickness, int yRadius = -1)
    {
        if (yRadius <= 0)
            yRadius = radius;

        Vector2 previous = Vector2.zero;
        bool hasPrevious = false;
        for (float a = startDegrees; a <= endDegrees; a += 4f)
        {
            float radians = Mathf.Deg2Rad * a;
            Vector2 point = new Vector2(cx + Mathf.Cos(radians) * radius, cy + Mathf.Sin(radians) * yRadius);
            if (hasPrevious)
                DrawLine(texture, Mathf.RoundToInt(previous.x), Mathf.RoundToInt(previous.y), Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), color, thickness);

            previous = point;
            hasPrevious = true;
        }
    }

    private static void DrawRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
    {
        DrawLine(texture, x, y, x + width, y, color, 1);
        DrawLine(texture, x + width, y, x + width, y + height, color, 1);
        DrawLine(texture, x + width, y + height, x, y + height, color, 1);
        DrawLine(texture, x, y + height, x, y, color, 1);
    }

    private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
    {
        for (int px = x; px < x + width; px++)
        {
            for (int py = y; py < y + height; py++)
                SetPixel(texture, px, py, color);
        }
    }

    private static void DrawCircle(Texture2D texture, int cx, int cy, int radius, Color32 color)
    {
        DrawArc(texture, cx, cy, radius, 0, 360, color, 2);
    }

    private static void FillCircle(Texture2D texture, int cx, int cy, int radius, Color32 color)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y <= radius * radius)
                    SetPixel(texture, cx + x, cy + y, color);
            }
        }
    }

    private static void FillEllipse(Texture2D texture, int cx, int cy, int rx, int ry, Color32 color)
    {
        for (int x = -rx; x <= rx; x++)
        {
            for (int y = -ry; y <= ry; y++)
            {
                float value = (x * x) / (float)(rx * rx) + (y * y) / (float)(ry * ry);
                if (value <= 1f)
                    SetPixel(texture, cx + x, cy + y, color);
            }
        }
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            for (int x = -thickness / 2; x <= thickness / 2; x++)
            {
                for (int y = -thickness / 2; y <= thickness / 2; y++)
                    SetPixel(texture, x0 + x, y0 + y, color);
            }

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static void SetPixel(Texture2D texture, int x, int y, Color32 color)
    {
        if (x < 0 || y < 0 || x >= IconSize || y >= IconSize)
            return;

        texture.SetPixel(x, y, color);
    }

    private static void EnsureFolder(string folder)
    {
        string normalized = folder.Replace("\\", "/");
        string[] parts = normalized.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private readonly struct OracleActionSpec
    {
        public readonly ActionType actionType;
        public readonly string name;
        public readonly int cost;
        public readonly string rangeText;
        public readonly string tooltipDescription;
        public readonly Sprite icon;

        public OracleActionSpec(ActionType actionType, string name, int cost, string rangeText, string tooltipDescription, Sprite icon)
        {
            this.actionType = actionType;
            this.name = name;
            this.cost = cost;
            this.rangeText = rangeText;
            this.tooltipDescription = tooltipDescription;
            this.icon = icon;
        }
    }

    private struct ButtonVisualRefs
    {
        public Image icon;
        public Text costText;
        public Text rangeText;
        public Image disabledOverlay;
        public Image cooldownOverlay;
        public Image usedOverlay;
        public Image energyRing;
        public Text tooltipTitle;
        public Text tooltipDescription;
    }
}
