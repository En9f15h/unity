using UnityEngine;
using UnityEngine.UI;

public partial class TurnPlanningManager
{
    private void EnsureEnemyActionRevealController()
    {
        if (enemyActionRevealController == null)
            enemyActionRevealController = GetComponent<EnemyActionRevealController>();

        if (enemyActionRevealController == null)
            enemyActionRevealController = gameObject.AddComponent<EnemyActionRevealController>();

        enemyActionRevealController.Bind(currentEnemyPreviewImages);
    }

    private void ResetEnemyActionRevealsForPlanning()
    {
        EnsureEnemyActionRevealController();
        enemyActionRevealController.ResetForPlanning(emptyPreviewSprite);
        RefreshSightViewsForPlanningRound();
    }

    private void PrepareEnemyActionsPreviewForResolution()
    {
        EnsureEnemyActionRevealController();

        int preservedLogicalIndex = -1;
        int preservedVisualIndex = -1;

        if (TryGetLocalSightRevealSlot(out int sightLogicalIndex))
        {
            preservedLogicalIndex = sightLogicalIndex;
            preservedVisualIndex = LogicalToVisualSlotIndex(
                sightLogicalIndex,
                currentEnemyPreviewImages.Count,
                mirrorEnemyPreviewSlotOrder
            );
        }

        enemyActionRevealController.PrepareForResolution(
            emptyPreviewSprite,
            preservedLogicalIndex,
            preservedVisualIndex
        );

        RefreshSightViewsForPlanningRound();
    }

    private void RevealEnemyActionForResolveStep(int logicalSlotIndex, int[] enemyActions)
    {
        EnsureEnemyActionRevealController();

        if (enemyActions == null || logicalSlotIndex < 0 || logicalSlotIndex >= enemyActions.Length)
            return;

        if (IsEnemyContinuationSlot(logicalSlotIndex, enemyActions, out _, out _))
        {
            HighlightEnemyRevealSlot(logicalSlotIndex);
            return;
        }

        ActionType action = ToSafeActionType(enemyActions[logicalSlotIndex], "EnemyProgressiveReveal", logicalSlotIndex);
        int slotCost = GetEnemyResolveSlotCost(action);
        slotCost = Mathf.Max(1, slotCost);

        for (int offset = 0; offset < slotCost; offset++)
        {
            int targetLogicalIndex = logicalSlotIndex + offset;
            if (targetLogicalIndex < 0 || targetLogicalIndex >= enemyActions.Length)
                break;

            int visualIndex = LogicalToVisualSlotIndex(
                targetLogicalIndex,
                currentEnemyPreviewImages.Count,
                mirrorEnemyPreviewSlotOrder
            );

            if (visualIndex < 0)
                continue;

            bool lockedContinuation = offset > 0;
            Sprite sprite = GetEnemyRevealSprite(action, targetLogicalIndex, lockedContinuation);

            enemyActionRevealController.RevealSlot(
                targetLogicalIndex,
                visualIndex,
                sprite,
                emptyPreviewSprite,
                lockedContinuation,
                true
            );
        }

        RevealClaimResultForEnemyAction(logicalSlotIndex, action, slotCost);
    }

    private void CompleteCurrentEnemyActionReveal()
    {
        if (enemyActionRevealController != null)
            enemyActionRevealController.CompleteCurrentReveal();
    }

    private void HighlightEnemyRevealSlot(int logicalSlotIndex)
    {
        if (enemyActionRevealController == null || currentEnemyPreviewImages == null)
            return;

        int visualIndex = LogicalToVisualSlotIndex(
            logicalSlotIndex,
            currentEnemyPreviewImages.Count,
            mirrorEnemyPreviewSlotOrder
        );

        enemyActionRevealController.HighlightSlot(visualIndex);
    }

    private bool IsEnemyContinuationSlot(int logicalSlotIndex, int[] enemyActions, out int sourceLogicalIndex, out ActionType sourceAction)
    {
        sourceLogicalIndex = -1;
        sourceAction = ActionType.None;

        if (enemyActions == null || logicalSlotIndex <= 0)
            return false;

        for (int i = 0; i < logicalSlotIndex; i++)
        {
            ActionType action = ToSafeActionType(enemyActions[i], "EnemyProgressiveReveal", i);
            if (action == ActionType.None)
                continue;

            int cost = GetEnemyResolveSlotCost(action);
            if (cost > 1 && i + cost > logicalSlotIndex)
            {
                sourceLogicalIndex = i;
                sourceAction = action;
                return true;
            }
        }

        return false;
    }

    private int GetEnemyResolveSlotCost(ActionType action)
    {
        if (action == ActionType.None)
            return 1;

        CharacterClassConfig config = GetEnemyClassConfig();
        ActionData actionData = config != null ? config.GetActionData(action) : null;
        return GetSlotCostForAction(actionData, action);
    }

    private Sprite GetEnemyRevealSprite(ActionType action, int logicalSlotIndex, bool lockedContinuation)
    {
        SlotSnapshot snapshot = new SlotSnapshot
        {
            logicalSlotIndex = logicalSlotIndex,
            actionId = (int)action,
            isEmpty = action == ActionType.None,
            isLockedContinuation = lockedContinuation,
            sourceLogicalSlotIndex = lockedContinuation ? FindEnemyContinuationSourceIndex(logicalSlotIndex) : -1,
            ownerActorNumber = enemyActorNumber,
            planningRound = 0,
            revision = 0
        };

        return GetSpriteForSnapshot(snapshot);
    }

    private int FindEnemyContinuationSourceIndex(int logicalSlotIndex)
    {
        if (logicalSlotIndex <= 0)
            return -1;

        return logicalSlotIndex - 1;
    }

    private void RevealClaimResultForEnemyAction(int logicalSlotIndex, ActionType actualAction, int slotCost)
    {
        RevealEnemyClaimResultForAction(logicalSlotIndex, actualAction, slotCost);
    }
}
