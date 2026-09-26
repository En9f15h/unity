using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

public static class OraclePrefabGenerator
{
    private const string KnightConfigPath = "Assets/Scripts/game/classes/KnightConfig.asset";
    private const string OracleConfigPath = "Assets/Scripts/game/classes/OracleConfig.asset";

    private const string KnightCharacterPrefabPath = "Assets/Resources/Prefab/knight/knight_0.prefab";
    private const string KnightGameplayUiPath = "Assets/Resources/Prefab/knight/knightUiPrefab.prefab";
    private const string SharedDancePrefabPath = "Assets/Resources/Prefab/Dance.prefab";

    private const string OraclePrefabFolder = "Assets/Resources/Prefab/Oracle";
    private const string OracleUiFolder = OraclePrefabFolder + "/UI";
    private const string OracleActionPrefabFolder = OracleUiFolder + "/Actions";
    private const string OracleSpriteFolder = OracleUiFolder + "/Sprites";
    private const string OracleCharacterFolder = "Assets/Resources/Character/Oracle";
    private const string OracleAnimationFolder = OracleCharacterFolder + "/Animations";
    private const string OracleAnimationClipFolder = OracleCharacterFolder + "/AnimationClips";

    private const string OracleCharacterPrefabPath = OraclePrefabFolder + "/Oracle_0.prefab";
    private const string OracleCharacterSpritePath = OracleCharacterFolder + "/Oracle.png";
    private const string OracleAnimatorControllerPath = OracleAnimationClipFolder + "/Oracle_Animator.controller";
    private const string OracleGameplayUiPath = OracleUiFolder + "/OracleGameplayUI.prefab";

    private const int OracleMaxHP = 25;
    private const int OracleSlotCount = 5;

    private static readonly OracleActionSpec[] OracleActions =
    {
        new OracleActionSpec(ActionType.Bolt, "Bolt", typeof(AttackActionData), ActionTemplate.LightAttack, 1, "2-4",
            "A ranged projectile that hits jumping targets. Can be blocked or parried."),
        new OracleActionSpec(ActionType.Rift, "Rift", typeof(AttackActionData), ActionTemplate.HeavyAttack, 2, "2-3",
            "A charged rift that ignores Parry and partially pierces Defense. Jump avoids it."),
        new OracleActionSpec(ActionType.Shift, "Shift", typeof(ActionData), ActionTemplate.Defense, 2, "Swap",
            "Vanish and shift behind the enemy after the swap point. Costs 2 slots, once per turn, and close Light Attack interrupts it."),
        new OracleActionSpec(ActionType.Ward, "Ward", typeof(DefenseActionData), ActionTemplate.Defense, 1, string.Empty,
            "Blocks Light Attack. Heavy Attack breaks through, and Low Attack bypasses it."),
        new OracleActionSpec(ActionType.Fade, "Fade", typeof(DefenseActionData), ActionTemplate.Defense, 2, "Max 4",
            "Move backward 2 cells, up to distance 4. Interrupted by close-range Light Attack."),
        new OracleActionSpec(ActionType.Sight, "Sight", typeof(UltimateActionData), ActionTemplate.Ultimate, 1, "Full",
            "Once per match. Starting next round, permanently reveal one fixed enemy action slot after the enemy is ready.")
    };

    private static readonly string[] RequiredAnimatorTriggers =
    {
        "MoveForward",
        "MoveBackward",
        "Jump",
        "Bolt",
        "RiftCharge",
        "RiftRelease",
        "Shift",
        "Ward",
        "Fade",
        "Sight",
        "Hit",
        "Defense",
        "Dance",
        "Ultimate"
    };

    private enum ExistingAssetMode
    {
        CreateMissingOnly,
        OverwriteExisting
    }

    private enum ActionTemplate
    {
        LightAttack,
        HeavyAttack,
        Defense,
        Ultimate
    }

    [MenuItem("Tools/Oracle/Create Oracle Prefabs")]
    public static void CreateFromMenu()
    {
        if (!TryChooseExistingAssetMode(out ExistingAssetMode mode))
            return;

        CreateOraclePrefabs(mode, false);
    }

    public static void CreateFromCommandLine()
    {
        bool overwrite = HasCommandLineFlag("-overwriteOraclePrefabs");
        ExistingAssetMode mode = overwrite ? ExistingAssetMode.OverwriteExisting : ExistingAssetMode.CreateMissingOnly;

        try
        {
            CreateOraclePrefabs(mode, true);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    private static void CreateOraclePrefabs(ExistingAssetMode mode, bool commandLine)
    {
        GenerationContext context = InspectKnightImplementation();
        Debug.Log("[OraclePrefabGenerator] Knight inspection before edits:\n" + context.inspectionReport);

        ValidateReferences(context);
        CreateOracleFolders();
        CacheOracleSharedSprites(context);

        GameObject oracleCharacterPrefab = CreateOracleCharacterPrefab(context, mode);
        Dictionary<ActionType, GameObject> actionPrefabs = CreateOracleActionPrefabs(context, mode);
        GameObject gameplayUiPrefab = EnsureOracleGameplayUi(mode);

        ConfigureOracleClassConfig(context, oracleCharacterPrefab, actionPrefabs, gameplayUiPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        OracleAnimatorSetup.BuildAnimator();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ValidateCreatedAssets();
        PrintCreationReport(context, oracleCharacterPrefab, actionPrefabs, gameplayUiPrefab, mode, commandLine);
    }

    private static bool TryChooseExistingAssetMode(out ExistingAssetMode mode)
    {
        bool hasExistingOracleAssets =
            File.Exists(OracleCharacterPrefabPath) ||
            File.Exists(OracleGameplayUiPath) ||
            Directory.Exists(OracleActionPrefabFolder);

        if (!hasExistingOracleAssets)
        {
            mode = ExistingAssetMode.CreateMissingOnly;
            return true;
        }

        int result = EditorUtility.DisplayDialogComplex(
            "Create Oracle Prefabs",
            "Oracle assets already exist. Choose whether to overwrite them or only create missing files.",
            "Overwrite Existing",
            "Create Missing Only",
            "Cancel");

        if (result == 2)
        {
            mode = ExistingAssetMode.CreateMissingOnly;
            return false;
        }

        mode = result == 0 ? ExistingAssetMode.OverwriteExisting : ExistingAssetMode.CreateMissingOnly;
        return true;
    }

    private static GenerationContext InspectKnightImplementation()
    {
        GenerationContext context = new GenerationContext();
        context.knightConfig = AssetDatabase.LoadAssetAtPath<CharacterClassConfig>(KnightConfigPath);
        context.knightCharacterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KnightCharacterPrefabPath);

        if (context.knightConfig != null)
        {
            context.lightAttackTemplate = context.knightConfig.lightAttack != null ? context.knightConfig.lightAttack.sourcePrefab : null;
            context.heavyAttackTemplate = context.knightConfig.heavyAttack != null ? context.knightConfig.heavyAttack.sourcePrefab : null;
            context.defenseTemplate = context.knightConfig.defense != null ? context.knightConfig.defense.sourcePrefab : null;
            context.ultimateTemplate = context.knightConfig.ultimate != null ? context.knightConfig.ultimate.sourcePrefab : null;
            context.energyBarPrefab = context.knightConfig.energyBarPrefab;
        }

        context.oracleConfig = AssetDatabase.LoadAssetAtPath<OracleClassConfig>(OracleConfigPath);
        context.inspectionReport = BuildInspectionReport(context);
        return context;
    }

    private static string BuildInspectionReport(GenerationContext context)
    {
        List<string> lines = new List<string>
        {
            "Knight character prefab: " + KnightCharacterPrefabPath,
            "Knight Light Attack prefab: " + GetAssetPath(context.lightAttackTemplate),
            "Knight Heavy Attack prefab: " + GetAssetPath(context.heavyAttackTemplate),
            "Knight Defense prefab: " + GetAssetPath(context.defenseTemplate),
            "Knight Ultimate prefab: " + GetAssetPath(context.ultimateTemplate),
            "Recommended Oracle character path: " + OracleCharacterPrefabPath,
            "Recommended Oracle action folder: " + OracleActionPrefabFolder,
            "Missing Oracle art/animation dependencies: final Oracle action art and full bespoke animation clips can replace current placeholders later."
        };

        if (context.knightCharacterPrefab != null)
        {
            lines.Add("Knight character root: " + context.knightCharacterPrefab.name +
                ", tag=" + context.knightCharacterPrefab.tag +
                ", layer=" + LayerMask.LayerToName(context.knightCharacterPrefab.layer));

            lines.Add("Knight character components: " + GetComponentSummary(context.knightCharacterPrefab));
            lines.Add("Knight hierarchy:");
            AppendHierarchy(lines, context.knightCharacterPrefab.transform, 0, 4, 80);
        }

        if (context.knightConfig != null)
        {
            lines.Add("Knight config: className=" + context.knightConfig.className +
                ", photonResourceFolder=" + context.knightConfig.photonResourceFolder +
                ", maxHP=" + context.knightConfig.maxHP +
                ", slotCount=" + context.knightConfig.slotCount);
        }

        lines.Add("Architecture note: action data is serialized inline on CharacterClassConfig; no separate ActionData assets are required.");
        return string.Join("\n", lines);
    }

    private static void AppendHierarchy(List<string> lines, Transform transform, int depth, int maxDepth, int maxLines)
    {
        if (transform == null || lines.Count >= maxLines)
            return;

        lines.Add(new string(' ', depth * 2) + "- " + transform.name + " [" + GetComponentSummary(transform.gameObject) + "]");

        if (depth >= maxDepth)
            return;

        for (int i = 0; i < transform.childCount && lines.Count < maxLines; i++)
            AppendHierarchy(lines, transform.GetChild(i), depth + 1, maxDepth, maxLines);
    }

    private static string GetComponentSummary(GameObject obj)
    {
        if (obj == null)
            return "missing";

        Component[] components = obj.GetComponents<Component>();
        List<string> names = new List<string>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
                names.Add("MissingScript");
            else
                names.Add(components[i].GetType().Name);
        }

        return string.Join(", ", names);
    }

    private static void ValidateReferences(GenerationContext context)
    {
        Require(context.knightConfig != null, "Missing Knight config: " + KnightConfigPath);
        Require(context.knightCharacterPrefab != null, "Missing Knight character prefab: " + KnightCharacterPrefabPath);
        Require(context.lightAttackTemplate != null, "Missing Knight light attack source prefab.");
        Require(context.heavyAttackTemplate != null, "Missing Knight heavy attack source prefab.");
        Require(context.defenseTemplate != null, "Missing Knight defense source prefab.");
        Require(context.ultimateTemplate != null, "Missing Knight ultimate source prefab.");

        if (context.knightCharacterPrefab != null)
        {
            Require(context.knightCharacterPrefab.GetComponent<CharacterUnit>() != null, "Knight prefab is missing CharacterUnit.");
            Require(context.knightCharacterPrefab.GetComponent<PhotonView>() != null, "Knight prefab is missing PhotonView.");
            Require(context.knightCharacterPrefab.GetComponentInChildren<Animator>(true) != null, "Knight prefab is missing Animator.");
            Require(context.knightCharacterPrefab.GetComponentInChildren<SpriteRenderer>(true) != null, "Knight prefab is missing SpriteRenderer.");
            Require(context.knightCharacterPrefab.GetComponent<Rigidbody2D>() != null, "Knight prefab is missing Rigidbody2D.");
            Require(context.knightCharacterPrefab.GetComponent<Collider2D>() != null, "Knight prefab is missing Collider2D.");
        }
    }

    private static void CreateOracleFolders()
    {
        EnsureFolder(OraclePrefabFolder);
        EnsureFolder(OracleUiFolder);
        EnsureFolder(OracleActionPrefabFolder);
        EnsureFolder(OracleSpriteFolder);
        EnsureFolder(OracleCharacterFolder);
        EnsureFolder(OracleAnimationFolder);
        EnsureFolder(OracleAnimationClipFolder);
    }

    private static GameObject CreateOracleCharacterPrefab(GenerationContext context, ExistingAssetMode mode)
    {
        if (File.Exists(OracleCharacterPrefabPath) && mode == ExistingAssetMode.CreateMissingOnly)
            return AssetDatabase.LoadAssetAtPath<GameObject>(OracleCharacterPrefabPath);

        GameObject root = PrefabUtility.LoadPrefabContents(KnightCharacterPrefabPath);
        try
        {
            root.name = "Oracle_0";
            root.tag = context.knightCharacterPrefab != null ? context.knightCharacterPrefab.tag : "Player";
            root.layer = context.knightCharacterPrefab != null ? context.knightCharacterPrefab.layer : 0;

            CharacterUnit unit = root.GetComponent<CharacterUnit>();
            if (unit != null)
            {
                unit.className = "Oracle";
                unit.skinName = "Oracle_0";
                unit.maxHP = OracleMaxHP;
                unit.currentHP = OracleMaxHP;
                unit.slotCount = OracleSlotCount;
                unit.isChargingHeavy = false;
                unit.heavyReleaseTurn = -1;
            }

            Transform visualRoot = FindVisualRoot(root);
            ApplyOracleVisualPlaceholder(visualRoot);

            Animator animator = visualRoot != null
                ? visualRoot.GetComponent<Animator>()
                : root.GetComponentInChildren<Animator>(true);

            AnimatorController oracleController = AssetDatabase.LoadAssetAtPath<AnimatorController>(OracleAnimatorControllerPath);
            if (animator != null && oracleController != null)
            {
                animator.runtimeAnimatorController = oracleController;
                animator.applyRootMotion = false;
            }

            if (unit != null && animator != null)
                SetSerializedReference(unit, "animatorOverride", animator);

            CharacterFacing facing = root.GetComponent<CharacterFacing>();
            if (facing != null && visualRoot != null)
                SetSerializedReference(facing, "visualRoot", visualRoot);

            ResetPhotonViewPrefabIds(root);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, OracleCharacterPrefabPath);
            Debug.Log("[OraclePrefabGenerator] Oracle character prefab saved: " + OracleCharacterPrefabPath);
            return prefab;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform FindVisualRoot(GameObject root)
    {
        if (root == null)
            return null;

        Transform named = root.transform.Find("visualRoot");
        if (named != null)
            return named;

        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator != null)
            return animator.transform;

        SpriteRenderer spriteRenderer = root.GetComponentInChildren<SpriteRenderer>(true);
        return spriteRenderer != null ? spriteRenderer.transform : root.transform;
    }

    private static void ApplyOracleVisualPlaceholder(Transform visualRoot)
    {
        if (visualRoot == null)
            return;

        Sprite oracleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(OracleCharacterSpritePath);
        if (oracleSprite == null)
            return;

        SpriteRenderer spriteRenderer = visualRoot.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer != null)
            spriteRenderer.sprite = oracleSprite;
    }

    private static void ResetPhotonViewPrefabIds(GameObject root)
    {
        PhotonView[] views = root.GetComponentsInChildren<PhotonView>(true);
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] == null)
                continue;

            SerializedObject serialized = new SerializedObject(views[i]);
            SerializedProperty viewId = serialized.FindProperty("sceneViewId");
            if (viewId != null)
                viewId.intValue = 0;

            SerializedProperty instantiationId = serialized.FindProperty("InstantiationId");
            if (instantiationId != null)
                instantiationId.intValue = 0;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static Dictionary<ActionType, GameObject> CreateOracleActionPrefabs(GenerationContext context, ExistingAssetMode mode)
    {
        Dictionary<ActionType, GameObject> actionPrefabs = new Dictionary<ActionType, GameObject>();

        for (int i = 0; i < OracleActions.Length; i++)
        {
            OracleActionSpec spec = OracleActions[i];
            string path = GetOracleActionPrefabPath(spec);
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (existing != null && mode == ExistingAssetMode.CreateMissingOnly)
            {
                actionPrefabs[spec.actionType] = existing;
                continue;
            }

            GameObject template = GetTemplatePrefab(context, spec.template);
            Require(template != null, "Missing template prefab for Oracle action " + spec.name);

            GameObject root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(template));
            try
            {
                root.name = "Oracle_" + spec.name;
                ConfigureActionPrefabRoot(root, spec, null, context);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject savedRoot = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureActionPrefabRoot(savedRoot, spec, savedPrefab, context);
                PrefabUtility.SaveAsPrefabAsset(savedRoot, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(savedRoot);
            }

            actionPrefabs[spec.actionType] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Debug.Log("[OraclePrefabGenerator] Oracle action prefab saved: " + path);
        }

        return actionPrefabs;
    }

    private static void CacheOracleSharedSprites(GenerationContext context)
    {
        context.disabledOverlaySprite = ResolveSpriteByName(context, "Oracle_DisabledOverlay");
        context.cooldownOverlaySprite = ResolveSpriteByName(context, "Oracle_CooldownOverlay");
        context.usedOverlaySprite = ResolveSpriteByName(context, "Oracle_UsedOverlay");
        context.riftContinuationSprite = ResolveSpriteByName(context, "Oracle_RiftContinuation");
        context.sightFrameSprite = ResolveSpriteByName(context, "Oracle_SightFrame");
    }

    private static void ConfigureActionPrefabRoot(
        GameObject root,
        OracleActionSpec spec,
        GameObject savedPrefab,
        GenerationContext context)
    {
        RectTransform rect = root.GetComponent<RectTransform>();
        if (rect == null)
            rect = root.AddComponent<RectTransform>();

        if (rect.sizeDelta == Vector2.zero)
            rect.sizeDelta = new Vector2(96f, 96f);

        Image rootImage = root.GetComponent<Image>();
        if (rootImage == null)
            rootImage = root.AddComponent<Image>();

        Sprite icon = ResolveActionIcon(context, spec, rootImage.sprite);
        if (icon != null)
            rootImage.sprite = icon;

        rootImage.raycastTarget = true;
        rootImage.preserveAspect = true;

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = root.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        if (root.GetComponent<DraggableItem>() == null)
            root.AddComponent<DraggableItem>();

        ActionDragData dragData = root.GetComponent<ActionDragData>();
        if (dragData == null)
            dragData = root.AddComponent<ActionDragData>();

        dragData.actionType = spec.actionType;

        ActionDragSource source = root.GetComponent<ActionDragSource>();
        if (source == null)
            source = root.AddComponent<ActionDragSource>();

        ActionData inlineData = CreateActionDataForSpec(spec);
        ApplyActionValues(inlineData, spec, savedPrefab, icon, context);

        SetSerializedReference(source, "dragPrefab", savedPrefab);
        SetPrivateField(source, "actionData", inlineData);
        SetPrivateField(source, "actionType", spec.actionType);

        ActionButtonView view = root.GetComponent<ActionButtonView>();
        if (view == null)
            view = root.AddComponent<ActionButtonView>();

        ButtonVisualRefs refs = EnsureButtonVisuals(root.transform, spec, icon, context);
        AssignButtonView(view, refs);
    }

    private static ButtonVisualRefs EnsureButtonVisuals(
        Transform parent,
        OracleActionSpec spec,
        Sprite icon,
        GenerationContext context)
    {
        ButtonVisualRefs refs = new ButtonVisualRefs();
        refs.icon = EnsureImage(parent, "Icon", icon, new Vector2(0.5f, 0.5f), new Vector2(72f, 72f), true, true);
        refs.costText = EnsureBadge(parent, "CostBadge", new Vector2(0f, 1f), new Vector2(22f, -22f), spec.slotCost.ToString());
        refs.rangeText = EnsureBadge(parent, "RangeBadge", new Vector2(1f, 0f), new Vector2(-28f, 22f), spec.rangeText);
        refs.disabledOverlay = EnsureImage(parent, "DisabledOverlay", context.disabledOverlaySprite, new Vector2(0.5f, 0.5f), new Vector2(96f, 96f), false, true);
        refs.cooldownOverlay = EnsureImage(parent, "CooldownOverlay", context.cooldownOverlaySprite, new Vector2(0.5f, 0.5f), new Vector2(96f, 96f), false, true);
        refs.usedOverlay = EnsureImage(parent, "UsedOverlay", context.usedOverlaySprite, new Vector2(0.5f, 0.5f), new Vector2(96f, 96f), false, true);
        refs.tooltipTitle = EnsureText(parent, "TooltipTitle", spec.name, new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(260f, 24f), 16);
        refs.tooltipDescription = EnsureText(parent, "TooltipDescription", spec.tooltipDescription, new Vector2(1f, 1f), new Vector2(8f, -24f), new Vector2(260f, 60f), 12);

        SetInactive(refs.disabledOverlay);
        SetInactive(refs.cooldownOverlay);
        SetInactive(refs.usedOverlay);

        if (refs.rangeText != null && string.IsNullOrEmpty(spec.rangeText))
            refs.rangeText.transform.parent.gameObject.SetActive(false);

        if (refs.tooltipTitle != null)
            refs.tooltipTitle.gameObject.SetActive(false);

        if (refs.tooltipDescription != null)
            refs.tooltipDescription.gameObject.SetActive(false);

        return refs;
    }

    private static Image EnsureImage(
        Transform parent,
        string name,
        Sprite sprite,
        Vector2 anchor,
        Vector2 size,
        bool preserveAspect,
        bool raycastOff)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.name = name;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;

        Image image = obj.GetComponent<Image>();
        if (image == null)
            image = obj.AddComponent<Image>();

        if (sprite != null)
            image.sprite = sprite;

        image.preserveAspect = preserveAspect;
        image.raycastTarget = !raycastOff;
        image.color = Color.white;
        obj.SetActive(true);
        return image;
    }

    private static Text EnsureBadge(
        Transform parent,
        string name,
        Vector2 anchor,
        Vector2 position,
        string text)
    {
        Transform existing = parent.Find(name);
        GameObject badge = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        badge.transform.SetParent(parent, false);
        badge.name = name;

        RectTransform rect = badge.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(34f, 22f);
        rect.localScale = Vector3.one;

        Image background = badge.GetComponent<Image>();
        if (background == null)
            background = badge.AddComponent<Image>();

        background.color = new Color(0.02f, 0.03f, 0.07f, 0.82f);
        background.raycastTarget = false;

        Text label = EnsureText(badge.transform, name.Replace("Badge", "Text"), text, Vector2.zero, Vector2.zero, rect.sizeDelta, 14);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.alignment = TextAnchor.MiddleCenter;
        badge.SetActive(true);
        return label;
    }

    private static Text EnsureText(
        Transform parent,
        string name,
        string text,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        int fontSize)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.name = name;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;

        Text label = obj.GetComponent<Text>();
        if (label == null)
            label = obj.AddComponent<Text>();

        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.UpperLeft;
        label.color = Color.white;
        label.raycastTarget = false;
        obj.SetActive(true);
        return label;
    }

    private static void SetInactive(Image image)
    {
        if (image != null)
            image.gameObject.SetActive(false);
    }

    private static void AssignButtonView(ActionButtonView view, ButtonVisualRefs refs)
    {
        SerializedObject serialized = new SerializedObject(view);
        SetSerializedReference(serialized, "iconImage", refs.icon);
        SetSerializedReference(serialized, "costText", refs.costText);
        SetSerializedReference(serialized, "rangeText", refs.rangeText);
        SetSerializedReference(serialized, "disabledOverlay", refs.disabledOverlay);
        SetSerializedReference(serialized, "cooldownOverlay", refs.cooldownOverlay);
        SetSerializedReference(serialized, "usedOverlay", refs.usedOverlay);
        SetSerializedReference(serialized, "tooltipTitleText", refs.tooltipTitle);
        SetSerializedReference(serialized, "tooltipDescriptionText", refs.tooltipDescription);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject EnsureOracleGameplayUi(ExistingAssetMode mode)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(OracleGameplayUiPath);
        if (existing != null)
            return existing;

        GameObject knightUi = AssetDatabase.LoadAssetAtPath<GameObject>(KnightGameplayUiPath);
        if (knightUi == null)
        {
            Debug.LogWarning("[OraclePrefabGenerator] Knight gameplay UI is missing; Oracle will use the scene fallback UI.");
            return null;
        }

        AssetDatabase.CopyAsset(KnightGameplayUiPath, OracleGameplayUiPath);
        Debug.Log("[OraclePrefabGenerator] Oracle gameplay UI copied from Knight template: " + OracleGameplayUiPath);
        return AssetDatabase.LoadAssetAtPath<GameObject>(OracleGameplayUiPath);
    }

    private static void ConfigureOracleClassConfig(
        GenerationContext context,
        GameObject oracleCharacterPrefab,
        Dictionary<ActionType, GameObject> actionPrefabs,
        GameObject gameplayUiPrefab)
    {
        OracleClassConfig config = context.oracleConfig;
        if (config == null)
        {
            EnsureFolder("Assets/Scripts/game/classes");
            config = ScriptableObject.CreateInstance<OracleClassConfig>();
            AssetDatabase.CreateAsset(config, OracleConfigPath);
        }

        context.oracleConfig = config;

        config.className = "Oracle";
        config.photonResourceFolder = "Oracle";
        config.maxHP = OracleMaxHP;
        config.slotCount = OracleSlotCount;
        config.skinNames = new[] { "Oracle_0" };
        config.skinPrefabs = new[] { oracleCharacterPrefab };
        config.energyBarPrefab = context.energyBarPrefab;
        config.gameplayUiPrefab = gameplayUiPrefab;

        CacheOracleSharedSprites(context);

        config.sightSelectedSlotFrame = context.sightFrameSprite;
        config.sightUsedOverlay = context.usedOverlaySprite;
        config.shiftCooldownOverlay = context.cooldownOverlaySprite;
        config.riftSecondarySlotLockedIcon = context.riftContinuationSprite;
        ClearInheritedCombatActions(config);
        ConfigureDanceAction(config, context);

        for (int i = 0; i < OracleActions.Length; i++)
        {
            OracleActionSpec spec = OracleActions[i];
            actionPrefabs.TryGetValue(spec.actionType, out GameObject sourcePrefab);
            ActionData actionData = EnsureOracleActionData(config, spec);
            ApplyActionValues(actionData, spec, sourcePrefab, ResolveActionIcon(context, spec, null), context);
        }

        EditorUtility.SetDirty(config);
        Debug.Log("[OraclePrefabGenerator] OracleConfig updated: " + OracleConfigPath);
    }

    private static void ConfigureDanceAction(OracleClassConfig config, GenerationContext context)
    {
        if (config == null)
            return;

        if (config.dance == null)
            config.dance = new DanceActionData();

        Sprite danceIcon = ResolveSpriteByName(context, "Dance");
        ActionData action = config.dance;
        action.actionName = "Dance";
        action.actionType = ActionType.Dance;
        action.sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharedDancePrefabPath);
        action.slotCost = 1;
        action.iconSprite = danceIcon;
        action.disabledOverlaySprite = context.disabledOverlaySprite;
        action.cooldownOverlaySprite = null;
        action.usedOverlaySprite = null;
        action.lockedContinuationSprite = danceIcon;
        action.tooltipTitle = "Dance";
        action.tooltipDescription = "Gain 1 energy.";
        action.rangeText = string.Empty;
        config.dance.energyGain = 1;
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

    private static ActionData EnsureOracleActionData(OracleClassConfig config, OracleActionSpec spec)
    {
        switch (spec.actionType)
        {
            case ActionType.Bolt:
                if (config.bolt == null)
                    config.bolt = new AttackActionData();
                return config.bolt;

            case ActionType.Rift:
                if (config.rift == null)
                    config.rift = new AttackActionData();
                return config.rift;

            case ActionType.Shift:
                if (config.shift == null)
                    config.shift = new ActionData();
                return config.shift;

            case ActionType.Ward:
                if (config.ward == null)
                    config.ward = new DefenseActionData();
                return config.ward;

            case ActionType.Fade:
                if (config.fade == null)
                    config.fade = new DefenseActionData();
                return config.fade;

            case ActionType.Sight:
                if (config.sight == null)
                    config.sight = new UltimateActionData();
                return config.sight;

            default:
                throw new InvalidOperationException("Unsupported Oracle action: " + spec.actionType);
        }
    }

    private static ActionData CreateActionDataForSpec(OracleActionSpec spec)
    {
        if (spec.dataType == typeof(AttackActionData))
            return new AttackActionData();

        if (spec.dataType == typeof(DefenseActionData))
            return new DefenseActionData();

        if (spec.dataType == typeof(UltimateActionData))
            return new UltimateActionData();

        return new ActionData();
    }

    private static void ApplyActionValues(
        ActionData action,
        OracleActionSpec spec,
        GameObject sourcePrefab,
        Sprite icon,
        GenerationContext context)
    {
        if (action == null)
            return;

        action.actionName = spec.name;
        action.actionType = spec.actionType;
        action.sourcePrefab = sourcePrefab;
        action.slotCost = spec.slotCost;
        action.iconSprite = icon;
        action.disabledOverlaySprite = context.disabledOverlaySprite;
        action.cooldownOverlaySprite = spec.actionType == ActionType.Fade || spec.actionType == ActionType.Shift ? context.cooldownOverlaySprite : null;
        action.usedOverlaySprite = spec.actionType == ActionType.Sight ? context.usedOverlaySprite : null;
        action.lockedContinuationSprite = spec.actionType == ActionType.Rift ? context.riftContinuationSprite : icon;
        action.tooltipTitle = spec.name;
        action.tooltipDescription = spec.tooltipDescription;
        action.rangeText = spec.rangeText;

        if (action is AttackActionData attack)
            ApplyAttackValues(attack, spec.actionType);

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

    private static void ApplyAttackValues(AttackActionData attack, ActionType actionType)
    {
        if (actionType == ActionType.Bolt)
        {
            attack.damage = 3;
            attack.minRange = 2;
            attack.range = 4;
            attack.canBeParried = true;
            attack.canBeDefended = true;
            attack.jumpInteraction = JumpInteractionType.None;
            attack.requiresCharge = false;
            attack.chargeTurns = 0;
            return;
        }

        if (actionType == ActionType.Rift)
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

    private static Sprite ResolveActionIcon(GenerationContext context, OracleActionSpec spec, Sprite fallback)
    {
        if (context.oracleConfig != null)
        {
            ActionData existingAction = context.oracleConfig.GetActionData(spec.actionType);
            if (existingAction != null && existingAction.iconSprite != null)
                return existingAction.iconSprite;
        }

        Sprite namedSprite = ResolveSpriteByName(context, spec.name);
        if (namedSprite != null)
            return namedSprite;

        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GetOracleActionPrefabPath(spec));
        Sprite existingPrefabSprite = FindSpriteInPrefab(existingPrefab, "Icon");
        if (existingPrefabSprite != null)
            return existingPrefabSprite;

        return fallback;
    }

    private static Sprite ResolveSpriteByName(GenerationContext context, string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (context.spriteCache.TryGetValue(name, out Sprite cached))
            return cached;

        string[] guids = AssetDatabase.FindAssets(name + " t:Sprite", new[] { OracleSpriteFolder, OracleCharacterFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                context.spriteCache[name] = sprite;
                return sprite;
            }
        }

        if (CanCreateFunctionalPlaceholderSprite(name))
            return CreateFunctionalPlaceholderSprite(context, name);

        return null;
    }

    private static bool CanCreateFunctionalPlaceholderSprite(string name)
    {
        return name == "Oracle_DisabledOverlay" ||
            name == "Oracle_CooldownOverlay" ||
            name == "Oracle_UsedOverlay" ||
            name == "Oracle_RiftContinuation" ||
            name == "Oracle_SightFrame";
    }

    private static Sprite CreateFunctionalPlaceholderSprite(GenerationContext context, string name)
    {
        string path = OracleSpriteFolder + "/" + name + ".png";

        if (!File.Exists(path))
        {
            Texture2D texture = DrawFunctionalPlaceholderSprite(name);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

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

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
            context.spriteCache[name] = sprite;

        Debug.Log("[OraclePrefabGenerator] Created functional placeholder sprite: " + path);
        return sprite;
    }

    private static Texture2D DrawFunctionalPlaceholderSprite(string name)
    {
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        texture.SetPixels32(pixels);

        if (name == "Oracle_DisabledOverlay")
        {
            FillRect(texture, 0, 0, size, size, new Color32(0, 0, 0, 150));
            DrawLine(texture, 22, 72, 74, 24, new Color32(210, 220, 240, 210), 5);
        }
        else if (name == "Oracle_CooldownOverlay")
        {
            FillRect(texture, 0, 0, size, size, new Color32(8, 12, 28, 130));
            DrawCircle(texture, 48, 48, 31, new Color32(80, 220, 255, 220));
            DrawLine(texture, 48, 48, 48, 24, new Color32(230, 210, 120, 230), 4);
            DrawLine(texture, 48, 48, 66, 57, new Color32(230, 210, 120, 230), 4);
        }
        else if (name == "Oracle_UsedOverlay")
        {
            FillRect(texture, 0, 0, size, size, new Color32(0, 0, 0, 175));
            DrawEye(texture, 48, 50, 48, 18, new Color32(235, 195, 90, 230));
            DrawLine(texture, 30, 68, 66, 32, new Color32(235, 195, 90, 230), 4);
        }
        else if (name == "Oracle_RiftContinuation")
        {
            DrawLine(texture, 12, 49, 34, 55, new Color32(210, 70, 255, 170), 5);
            DrawLine(texture, 34, 55, 58, 39, new Color32(170, 120, 255, 170), 5);
            DrawLine(texture, 58, 39, 84, 48, new Color32(210, 70, 255, 170), 5);
            DrawCircle(texture, 48, 48, 30, new Color32(210, 70, 255, 120));
        }
        else if (name == "Oracle_SightFrame")
        {
            DrawCircle(texture, 48, 48, 42, new Color32(235, 195, 90, 240));
            DrawCircle(texture, 48, 48, 35, new Color32(145, 110, 255, 220));
            DrawEye(texture, 48, 48, 50, 18, new Color32(235, 195, 90, 220));
        }

        texture.Apply();
        return texture;
    }

    private static void DrawEye(Texture2D texture, int cx, int cy, int width, int height, Color32 color)
    {
        DrawArc(texture, cx, cy, width / 2, height / 2, 0, 180, color, 2);
        DrawArc(texture, cx, cy, width / 2, height / 2, 180, 360, color, 2);
        FillCircle(texture, cx, cy, 4, color);
    }

    private static void DrawCircle(Texture2D texture, int cx, int cy, int radius, Color32 color)
    {
        DrawArc(texture, cx, cy, radius, radius, 0, 360, color, 2);
    }

    private static void DrawArc(
        Texture2D texture,
        int cx,
        int cy,
        int radiusX,
        int radiusY,
        float startDegrees,
        float endDegrees,
        Color32 color,
        int thickness)
    {
        Vector2 previous = Vector2.zero;
        bool hasPrevious = false;
        for (float a = startDegrees; a <= endDegrees; a += 4f)
        {
            float radians = Mathf.Deg2Rad * a;
            Vector2 point = new Vector2(cx + Mathf.Cos(radians) * radiusX, cy + Mathf.Sin(radians) * radiusY);
            if (hasPrevious)
                DrawLine(texture, Mathf.RoundToInt(previous.x), Mathf.RoundToInt(previous.y), Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), color, thickness);

            previous = point;
            hasPrevious = true;
        }
    }

    private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
    {
        for (int px = x; px < x + width; px++)
        {
            for (int py = y; py < y + height; py++)
                SetPixel(texture, px, py, color);
        }
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
        if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            return;

        texture.SetPixel(x, y, color);
    }

    private static Sprite FindSpriteInPrefab(GameObject prefab, string preferredChildName)
    {
        if (prefab == null)
            return null;

        Image[] images = prefab.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].sprite != null && images[i].name == preferredChildName)
                return images[i].sprite;
        }

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].sprite != null)
                return images[i].sprite;
        }

        return null;
    }

    private static GameObject GetTemplatePrefab(GenerationContext context, ActionTemplate template)
    {
        switch (template)
        {
            case ActionTemplate.LightAttack:
                return context.lightAttackTemplate;

            case ActionTemplate.HeavyAttack:
                return context.heavyAttackTemplate;

            case ActionTemplate.Defense:
                return context.defenseTemplate;

            case ActionTemplate.Ultimate:
                return context.ultimateTemplate;

            default:
                return null;
        }
    }

    private static string GetOracleActionPrefabPath(OracleActionSpec spec)
    {
        return OracleActionPrefabFolder + "/Oracle_" + spec.name + ".prefab";
    }

    private static void ValidateCreatedAssets()
    {
        List<string> failures = new List<string>();
        OracleClassConfig config = AssetDatabase.LoadAssetAtPath<OracleClassConfig>(OracleConfigPath);
        GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OracleCharacterPrefabPath);

        Require(config != null, failures, "OracleConfig is missing.");
        Require(characterPrefab != null, failures, "Oracle character prefab is missing.");

        if (config != null)
        {
            Require(config.className == "Oracle", failures, "Oracle className must be Oracle.");
            Require(config.photonResourceFolder == "Oracle", failures, "Oracle photonResourceFolder must be Oracle.");
            Require(config.maxHP == OracleMaxHP, failures, "Oracle maxHP must be " + OracleMaxHP + ".");
            Require(config.slotCount == OracleSlotCount, failures, "Oracle slotCount must be " + OracleSlotCount + ".");
            Require(config.skinPrefabs != null && config.skinPrefabs.Length > 0 && config.skinPrefabs[0] == characterPrefab, failures, "Oracle skinPrefabs must point to Oracle_0.");
            Require(config.energyBarPrefab != null, failures, "Oracle energyBarPrefab is missing.");
        }

        if (characterPrefab != null)
        {
            Require(characterPrefab.CompareTag("Player"), failures, "Oracle character prefab must use Player tag.");
            Require(characterPrefab.GetComponent<CharacterUnit>() != null, failures, "Oracle character prefab is missing CharacterUnit.");
            Require(characterPrefab.GetComponent<PhotonView>() != null, failures, "Oracle character prefab is missing PhotonView.");
            Require(characterPrefab.GetComponent<PhotonTransformView>() != null, failures, "Oracle character prefab is missing PhotonTransformView.");
            Require(characterPrefab.GetComponent<Rigidbody2D>() != null, failures, "Oracle character prefab is missing Rigidbody2D.");
            Require(characterPrefab.GetComponent<Collider2D>() != null, failures, "Oracle character prefab is missing Collider2D.");
            Require(characterPrefab.GetComponentInChildren<SpriteRenderer>(true) != null, failures, "Oracle character prefab is missing SpriteRenderer.");

            Animator animator = characterPrefab.GetComponentInChildren<Animator>(true);
            Require(animator != null, failures, "Oracle character prefab is missing Animator.");
            ValidateAnimatorParameters(animator, failures);
            ValidateNoMissingScripts(characterPrefab.transform, failures);
        }

        if (config != null)
        {
            ValidateActionData(config.bolt, ActionType.Bolt, "Bolt", typeof(AttackActionData), failures);
            ValidateActionData(config.rift, ActionType.Rift, "Rift", typeof(AttackActionData), failures);
            ValidateActionData(config.shift, ActionType.Shift, "Shift", typeof(ActionData), failures);
            ValidateActionData(config.ward, ActionType.Ward, "Ward", typeof(DefenseActionData), failures);
            ValidateActionData(config.fade, ActionType.Fade, "Fade", typeof(DefenseActionData), failures);
            ValidateActionData(config.sight, ActionType.Sight, "Sight", typeof(UltimateActionData), failures);
        }

        if (failures.Count > 0)
        {
            for (int i = 0; i < failures.Count; i++)
                Debug.LogError("[OraclePrefabGenerator] Validation failed: " + failures[i]);

            throw new InvalidOperationException("Oracle prefab validation failed. See Console for details.");
        }

        Debug.Log("[OraclePrefabGenerator] Created Oracle assets passed prefab validation.");
    }

    private static void ValidateActionData(
        ActionData actionData,
        ActionType actionType,
        string name,
        Type expectedType,
        List<string> failures)
    {
        Require(actionData != null, failures, name + " action data is missing.");
        if (actionData == null)
            return;

        Require(expectedType.IsInstanceOfType(actionData), failures, name + " action data type must be " + expectedType.Name + ".");
        Require(actionData.actionName == name, failures, name + " actionName is incorrect.");
        Require(actionData.actionType == actionType, failures, name + " actionType is incorrect.");
        Require(actionData.sourcePrefab != null, failures, name + " sourcePrefab is missing.");
        Require(actionData.iconSprite != null, failures, name + " iconSprite is missing.");
        Require(actionData.disabledOverlaySprite != null, failures, name + " disabledOverlaySprite is missing.");

        if (actionData.sourcePrefab != null)
        {
            Require(actionData.sourcePrefab.GetComponent<ActionDragSource>() != null, failures, name + " sourcePrefab is missing ActionDragSource.");
            Require(actionData.sourcePrefab.GetComponent<CanvasGroup>() != null, failures, name + " sourcePrefab is missing CanvasGroup.");
            Require(actionData.sourcePrefab.GetComponent<DraggableItem>() != null, failures, name + " sourcePrefab is missing DraggableItem.");
            ValidateNoMissingScripts(actionData.sourcePrefab.transform, failures);
        }
    }

    private static void ValidateAnimatorParameters(Animator animator, List<string> failures)
    {
        AnimatorController controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
        Require(controller != null, failures, "Oracle Animator must use an AnimatorController.");

        if (controller == null)
            return;

        for (int i = 0; i < RequiredAnimatorTriggers.Length; i++)
        {
            bool found = false;
            for (int j = 0; j < controller.parameters.Length; j++)
            {
                AnimatorControllerParameter parameter = controller.parameters[j];
                if (parameter.name == RequiredAnimatorTriggers[i] &&
                    parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    found = true;
                    break;
                }
            }

            Require(found, failures, "Oracle Animator is missing trigger: " + RequiredAnimatorTriggers[i]);
        }
    }

    private static void ValidateNoMissingScripts(Transform transform, List<string> failures)
    {
        if (transform == null)
            return;

        Component[] components = transform.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
                failures.Add("Missing script on " + GetHierarchyPath(transform));
        }

        for (int i = 0; i < transform.childCount; i++)
            ValidateNoMissingScripts(transform.GetChild(i), failures);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return "<null>";

        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }

    private static void PrintCreationReport(
        GenerationContext context,
        GameObject oracleCharacterPrefab,
        Dictionary<ActionType, GameObject> actionPrefabs,
        GameObject gameplayUiPrefab,
        ExistingAssetMode mode,
        bool commandLine)
    {
        List<string> lines = new List<string>
        {
            "[OraclePrefabGenerator] Creation report",
            "Mode: " + mode,
            "Oracle character prefab: " + GetAssetPath(oracleCharacterPrefab),
            "Oracle gameplay UI prefab: " + GetAssetPath(gameplayUiPrefab),
            "Oracle config: " + OracleConfigPath,
            "Unity menu: Tools/Oracle/Create Oracle Prefabs",
            "Animator parameters: " + string.Join(", ", RequiredAnimatorTriggers),
            "Manual references remaining: replace placeholder Oracle sprites, clips, and effects when final art is ready."
        };

        for (int i = 0; i < OracleActions.Length; i++)
        {
            OracleActionSpec spec = OracleActions[i];
            actionPrefabs.TryGetValue(spec.actionType, out GameObject prefab);
            lines.Add(spec.name + " prefab: " + GetAssetPath(prefab));
        }

        if (commandLine)
            lines.Add("Command-line overwrite flag: -overwriteOraclePrefabs");

        Debug.Log(string.Join("\n", lines));
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

    private static void SetSerializedReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        if (target == null)
            return;

        SerializedObject serialized = new SerializedObject(target);
        SetSerializedReference(serialized, propertyName, value);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetSerializedReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null)
            return;

        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
            field.SetValue(target, value);

        if (target is UnityEngine.Object unityObject)
            EditorUtility.SetDirty(unityObject);
    }

    private static string GetAssetPath(UnityEngine.Object obj)
    {
        return obj != null ? AssetDatabase.GetAssetPath(obj) : "<missing>";
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, List<string> failures, string message)
    {
        if (!condition)
            failures.Add(message);
    }

    private static bool HasCommandLineFlag(string flag)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private sealed class GenerationContext
    {
        public CharacterClassConfig knightConfig;
        public OracleClassConfig oracleConfig;
        public GameObject knightCharacterPrefab;
        public GameObject lightAttackTemplate;
        public GameObject heavyAttackTemplate;
        public GameObject defenseTemplate;
        public GameObject ultimateTemplate;
        public GameObject energyBarPrefab;
        public Sprite disabledOverlaySprite;
        public Sprite cooldownOverlaySprite;
        public Sprite usedOverlaySprite;
        public Sprite riftContinuationSprite;
        public Sprite sightFrameSprite;
        public string inspectionReport;
        public readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    }

    private readonly struct OracleActionSpec
    {
        public readonly ActionType actionType;
        public readonly string name;
        public readonly Type dataType;
        public readonly ActionTemplate template;
        public readonly int slotCost;
        public readonly string rangeText;
        public readonly string tooltipDescription;

        public OracleActionSpec(
            ActionType actionType,
            string name,
            Type dataType,
            ActionTemplate template,
            int slotCost,
            string rangeText,
            string tooltipDescription)
        {
            this.actionType = actionType;
            this.name = name;
            this.dataType = dataType;
            this.template = template;
            this.slotCost = slotCost;
            this.rangeText = rangeText;
            this.tooltipDescription = tooltipDescription;
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
        public Text tooltipTitle;
        public Text tooltipDescription;
    }
}
