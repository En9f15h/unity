using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public partial class TurnPlanningManager
{
    private void SetupEnemyPreviewSide()
    {
        bool isHost = PhotonNetwork.IsMasterClient;

        if (isHost)
        {
            currentEnemyPreviewRoot = rightEnemyPreviewRoot;

            if (leftEnemyPreviewRoot != null)
                leftEnemyPreviewRoot.gameObject.SetActive(false);

            if (rightEnemyPreviewRoot != null)
                rightEnemyPreviewRoot.gameObject.SetActive(true);
        }
        else
        {
            currentEnemyPreviewRoot = leftEnemyPreviewRoot;

            if (leftEnemyPreviewRoot != null)
                leftEnemyPreviewRoot.gameObject.SetActive(true);

            if (rightEnemyPreviewRoot != null)
                rightEnemyPreviewRoot.gameObject.SetActive(false);
        }

    }

    private void CaptureInitialActionDisplayLayoutPositions()
    {
        ResolvePlanningSlotRoots();

        bool rootsChanged =
            capturedHostSlotRoot != hostSlotRoot ||
            capturedClientSlotRoot != clientSlotRoot ||
            capturedLeftEnemyPreviewRoot != leftEnemyPreviewRoot ||
            capturedRightEnemyPreviewRoot != rightEnemyPreviewRoot;

        if (capturedInitialActionDisplayLayoutPositions && !rootsChanged)
            return;

        capturedHostSlotRoot = hostSlotRoot;
        capturedClientSlotRoot = clientSlotRoot;
        capturedLeftEnemyPreviewRoot = leftEnemyPreviewRoot;
        capturedRightEnemyPreviewRoot = rightEnemyPreviewRoot;

        if (hostSlotRoot != null)
            hostSlotRootInitialAnchoredPosition = hostSlotRoot.anchoredPosition;

        if (clientSlotRoot != null)
            clientSlotRootInitialAnchoredPosition = clientSlotRoot.anchoredPosition;

        if (leftEnemyPreviewRoot != null)
            leftEnemyPreviewRootInitialAnchoredPosition = leftEnemyPreviewRoot.anchoredPosition;

        if (rightEnemyPreviewRoot != null)
            rightEnemyPreviewRootInitialAnchoredPosition = rightEnemyPreviewRoot.anchoredPosition;

        capturedInitialActionDisplayLayoutPositions =
            hostSlotRoot != null ||
            clientSlotRoot != null ||
            leftEnemyPreviewRoot != null ||
            rightEnemyPreviewRoot != null;
    }

    private void ApplyActionDisplayLayout()
    {
        CaptureInitialActionDisplayLayoutPositions();

        CenterSlotRootForActionDisplay(hostSlotRoot);
        CenterSlotRootForActionDisplay(clientSlotRoot);
        PositionEnemyPreviewBelowSlotRoots();
        actionDisplayLayoutActive = true;
        ApplyClaimActionDisplayLayout();
    }

    private void RestoreActionDisplayLayout()
    {
        ResolvePlanningSlotRoots();

        if (!capturedInitialActionDisplayLayoutPositions)
        {
            actionDisplayLayoutActive = false;
            RestoreClaimActionDisplayLayout();
            return;
        }

        if (hostSlotRoot != null)
            hostSlotRoot.anchoredPosition = hostSlotRootInitialAnchoredPosition;

        if (clientSlotRoot != null)
            clientSlotRoot.anchoredPosition = clientSlotRootInitialAnchoredPosition;

        if (leftEnemyPreviewRoot != null)
            leftEnemyPreviewRoot.anchoredPosition = leftEnemyPreviewRootInitialAnchoredPosition;

        if (rightEnemyPreviewRoot != null)
            rightEnemyPreviewRoot.anchoredPosition = rightEnemyPreviewRootInitialAnchoredPosition;

        actionDisplayLayoutActive = false;
        RestoreClaimActionDisplayLayout();
    }

    private void CenterSlotRootForActionDisplay(RectTransform slotRoot)
    {
        if (slotRoot == null)
            return;

        Vector2 anchoredPosition = slotRoot.anchoredPosition;
        anchoredPosition.x = 0f;
        slotRoot.anchoredPosition = anchoredPosition;
    }

    private void PositionEnemyPreviewBelowSlotRoots()
    {
        ResolvePlanningSlotRoots();
        PositionEnemyPreviewBelowSlotRoot(leftEnemyPreviewRoot, hostSlotRoot);
        PositionEnemyPreviewBelowSlotRoot(rightEnemyPreviewRoot, clientSlotRoot);
    }

    private void PositionEnemyPreviewBelowSlotRoot(RectTransform previewRoot, RectTransform slotRoot)
    {
        if (previewRoot == null || slotRoot == null)
            return;

        Vector2 anchoredPosition = slotRoot.anchoredPosition;
        anchoredPosition.y -= enemyPreviewBelowSlotRootOffset;
        previewRoot.anchoredPosition = anchoredPosition;
    }

    private void ResolvePlanningSlotRoots()
    {
        if (hostSlotRoot == null)
            hostSlotRoot = FindRectTransformByName("hostslotroot", "masterslotroot", "myslotroot");

        if (clientSlotRoot == null)
            clientSlotRoot = FindRectTransformByName("clientslotroot", "enemyslotroot", "opponentslotroot");
    }

    private RectTransform FindRectTransformByName(params string[] tokens)
    {
        RectTransform[] rectTransforms = FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (rectTransform == null)
                continue;

            string normalizedName = NormalizeUiName(rectTransform.name);
            for (int j = 0; j < tokens.Length; j++)
            {
                if (normalizedName.Contains(tokens[j], StringComparison.OrdinalIgnoreCase))
                    return rectTransform;
            }
        }

        return null;
    }

    private string NormalizeUiName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private void RebuildEnemyPreviewSlots(int slotCount)
    {
        if (currentEnemyPreviewRoot == null)
        {
            Debug.LogWarning("currentEnemyPreviewRoot is missing; cannot rebuild enemy preview slots.");
            return;
        }

        if (previewCellPrefab == null)
        {
            Debug.LogWarning("previewCellPrefab is missing.");
            return;
        }

        for (int i = currentEnemyPreviewRoot.childCount - 1; i >= 0; i--)
            Destroy(currentEnemyPreviewRoot.GetChild(i).gameObject);

        currentEnemyPreviewImages.Clear();
        currentEnemyPreviewSightViews.Clear();

        for (int i = 0; i < slotCount; i++)
        {
            GameObject cell = Instantiate(previewCellPrefab, currentEnemyPreviewRoot);
            cell.name = "EnemyPreview_" + i;

            RectTransform rect = cell.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }

            SightSlotView sightView = GetOrCreateSightSlotView(cell);
            if (sightView != null)
            {
                Image img = sightView.GetOrCreateActionIconImage();
                if (img != null)
                {
                    img.sprite = emptyPreviewSprite;
                    img.color = Color.white;
                    img.enabled = true;
                    currentEnemyPreviewImages.Add(img);
                    sightView.SetWatchedSpriteTarget(img);
                }

                sightView.SetWatched(false);
                sightView.ClearReveal();
                currentEnemyPreviewSightViews.Add(sightView);
            }
            else
            {
                Image img = cell.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = emptyPreviewSprite;
                    img.color = Color.white;
                    currentEnemyPreviewImages.Add(img);
                }
            }
        }

        RefreshSightViewsForPlanningRound();
        EnsureClaimControllerForCurrentUi();
    }

    private void BuildEnemyPreviewSpriteMap(CharacterClassConfig config)
    {
        enemyPreviewSpriteMap.Clear();

        if (config == null)
        {
            Debug.LogWarning("BuildEnemyPreviewSpriteMap: config is missing.");
            return;
        }

        ActionData[] classActions = config.GetClassActions();
        if (classActions != null)
        {
            for (int i = 0; i < classActions.Length; i++)
                RegisterActionPreviewSprite(classActions[i]);
        }

        RegisterSharedActionPreviewSprite(ActionType.MoveForward, moveForwardAction, "Prefab/walkFornt");
        RegisterSharedActionPreviewSprite(ActionType.MoveBackward, moveBackwardAction, "Prefab/walkBack");
        RegisterSharedActionPreviewSprite(ActionType.Jump, jumpAction, "Prefab/jump");
    }

    private void RegisterActionPreviewSprite(ActionData actionData)
    {
        if (actionData == null)
            return;

        Sprite sprite = GetPreviewSpriteFromAction(actionData);

        if (sprite == null)
        {
            Debug.LogWarning("Missing preview sprite for action: " + actionData.actionType);
            return;
        }

        enemyPreviewSpriteMap[actionData.actionType] = sprite;
    }

    private Sprite GetPreviewSpriteFromAction(ActionData actionData)
    {
        if (actionData == null)
            return null;

        if (actionData.iconSprite != null)
            return actionData.iconSprite;

        return GetPreviewSpriteFromPrefab(actionData.sourcePrefab);
    }

    private void RegisterSharedActionPreviewSprite(ActionType actionType, ActionData actionData, string resourcesPath)
    {
        Sprite sprite = null;

        if (actionData != null && actionData.actionType == actionType)
            sprite = GetPreviewSpriteFromPrefab(actionData.sourcePrefab);

        if (sprite == null && !string.IsNullOrEmpty(resourcesPath))
        {
            GameObject fallbackPrefab = Resources.Load<GameObject>(resourcesPath);
            sprite = GetPreviewSpriteFromPrefab(fallbackPrefab);
        }

        if (sprite == null)
        {
            Debug.LogWarning("Missing preview sprite for shared action: " + actionType);
            return;
        }

        enemyPreviewSpriteMap[actionType] = sprite;
    }

    private Sprite GetPreviewSpriteFromPrefab(GameObject sourcePrefab)
    {
        if (sourcePrefab == null)
            return null;

        Image img = sourcePrefab.GetComponent<Image>();

        if (img == null || img.sprite == null)
            img = sourcePrefab.GetComponentInChildren<Image>(true);

        return img != null ? img.sprite : null;
    }

    private void ShowEnemyActionsPreview(int[] enemyActions)
    {
        if (currentEnemyPreviewImages == null || currentEnemyPreviewImages.Count == 0)
            return;

        ClearEnemyActionsPreview();

        int previewIndex = 0;
        int actionIndex = 0;

        while (actionIndex < enemyActions.Length && previewIndex < currentEnemyPreviewImages.Count)
        {
            ActionType action = GetEnemyPreviewDisplayAction((ActionType)enemyActions[actionIndex]);

            SetPreviewImage(currentEnemyPreviewImages[previewIndex], action);

            int cost = GetSlotCost(action);

            if (cost == 2 && previewIndex + 1 < currentEnemyPreviewImages.Count)
                SetPreviewImage(currentEnemyPreviewImages[previewIndex + 1], action);

            previewIndex += cost;
            actionIndex += cost;
        }

        RefreshSightViewsForPlanningRound();
        EnsureEnemyActionRevealController();
    }

    private ActionType GetEnemyPreviewDisplayAction(ActionType action)
    {
        // Action identity is semantic, not screen direction. Slot order may be mirrored elsewhere,
        // but MoveForward must still display the MoveForward icon.
        return action;
    }

    private void ClearEnemyActionsPreview()
    {
        if (currentEnemyPreviewImages == null) return;

        for (int i = 0; i < currentEnemyPreviewImages.Count; i++)
        {
            if (currentEnemyPreviewImages[i] == null) continue;

            currentEnemyPreviewImages[i].sprite = emptyPreviewSprite;
            currentEnemyPreviewImages[i].color = Color.white;

            if (i < currentEnemyPreviewSightViews.Count && currentEnemyPreviewSightViews[i] != null)
                currentEnemyPreviewSightViews[i].ClearReveal();
        }

        RefreshSightViewsForPlanningRound();
    }

    private void SetPreviewImage(Image target, ActionType action)
    {
        if (target == null) return;

        Sprite sprite = GetPreviewSprite(action);
        target.sprite = sprite != null ? sprite : emptyPreviewSprite;
        target.color = Color.white;
    }

    private Sprite GetPreviewSprite(ActionType action)
    {
        if (enemyPreviewSpriteMap.TryGetValue(action, out Sprite sprite))
            return sprite;

        return null;
    }

}
