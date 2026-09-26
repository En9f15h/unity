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
    private bool IsHeavyChargingThisStep(ActionType effectiveAction, bool heavyReleaseNow)
    {
        return effectiveAction == ActionType.HeavyAttack && !heavyReleaseNow;
    }

    private bool IsHeavyAnimationProtectedThisStep(ActionType effectiveAction)
    {
        return effectiveAction == ActionType.HeavyAttack;
    }

    private bool IsRiftChargingThisStep(ActionType effectiveAction, bool riftReleaseNow)
    {
        return effectiveAction == ActionType.Rift && !riftReleaseNow;
    }

    private bool IsJumpAction(ActionType action)
    {
        return action == ActionType.Jump;
    }

    private bool IsShiftAction(ActionType action)
    {
        return action == ActionType.Shift;
    }

    private bool IsFadeAction(ActionType action)
    {
        return action == ActionType.Fade;
    }

    private bool IsRiftAction(ActionType action)
    {
        return action == ActionType.Rift;
    }

    private bool IsLightAttack(ActionType action)
    {
        return action == ActionType.LightAttack;
    }

    private bool IsHeavyAttack(ActionType action)
    {
        return action == ActionType.HeavyAttack;
    }

    private bool IsConsecutiveJump(ActionType previousEffectiveAction, ActionType currentAction)
    {
        return IsJumpAction(previousEffectiveAction) && IsJumpAction(currentAction);
    }

    private int GetActionPriority(ActionType action)
    {
        if (IsLightAttack(action))
            return 2;

        if (IsHeavyAttack(action))
            return 0;

        return 1;
    }

    private bool CanLightAttackInterruptHeavy(ActionType lightAction, ActionType heavyAction)
    {
        return IsLightAttack(lightAction) &&
               IsHeavyAttack(heavyAction) &&
               GetActionPriority(lightAction) > GetActionPriority(heavyAction);
    }

    private bool CanLightAttackInterruptOracleAction(
        ActionType lightAction,
        ActionType oracleAction,
        bool oracleIsMine,
        bool oracleReleaseNow)
    {
        if (!IsLightAttack(lightAction) || !IsOracleClass(oracleIsMine))
            return false;

        if (IsFadeAction(oracleAction))
            return GetCurrentGridDistance() <= 1;

        if (IsShiftAction(oracleAction))
            return GetCurrentGridDistance() <= 1;

        if (IsRiftAction(oracleAction) && !oracleReleaseNow)
            return GetCurrentGridDistance() <= 1;

        return false;
    }

    public bool WouldCreateConsecutiveJump(ActionType actionType, ActionSlot targetSlot, ActionSlot movingFromSlot = null)
    {
        if (!IsJumpAction(actionType) || targetSlot == null)
            return false;

        ActionType previousEffectiveAction = GetPreviousEffectiveSlotAction(targetSlot, movingFromSlot);
        return IsConsecutiveJump(previousEffectiveAction, actionType);
    }

    private ActionType GetPreviousEffectiveSlotAction(ActionSlot targetSlot, ActionSlot movingFromSlot)
    {
        if (planningSlots == null || targetSlot == null)
            return ActionType.None;

        ActionSlot previousSlot = null;
        int previousIndex = int.MinValue;

        for (int i = 0; i < planningSlots.Length; i++)
        {
            ActionSlot slot = planningSlots[i];
            if (slot == null || slot == targetSlot || slot == movingFromSlot)
                continue;

            // Multi-slot extension slots are not valid actions, so skip them when checking the previous effective action.
            if (slot.IsOccupiedByHeavyExtension)
                continue;

            if (slot.slotIndex >= targetSlot.slotIndex || slot.slotIndex <= previousIndex)
                continue;

            if (!slot.HasPlacedItem())
                continue;

            ActionType slotAction = slot.GetCurrentActionType();
            if (slotAction == ActionType.None)
                continue;

            previousSlot = slot;
            previousIndex = slot.slotIndex;
        }

        return previousSlot != null ? previousSlot.GetCurrentActionType() : ActionType.None;
    }

    private int[] SanitizeConsecutiveJumpsForResolve(int[] sourceActions, string ownerLabel)
    {
        if (sourceActions == null)
            return new int[0];

        int[] sanitizedActions = new int[sourceActions.Length];
        Array.Copy(sourceActions, sanitizedActions, sourceActions.Length);

        ActionType previousEffectiveAction = ActionType.None;

        for (int i = 0; i < sanitizedActions.Length; i++)
        {
            ActionType currentAction = ToSafeActionType(sanitizedActions[i], ownerLabel, i);
            sanitizedActions[i] = (int)currentAction;

            if (IsConsecutiveJump(previousEffectiveAction, currentAction))
            {
                sanitizedActions[i] = (int)ActionType.None;
                Debug.LogWarning($"[TurnPlanningManager] Consecutive Jump detected for {ownerLabel} at slot {i + 1}; treating it as None.");
                continue;
            }

            // None and multi-slot extension slots do not update the previous effective action.
            if (currentAction != ActionType.None)
                previousEffectiveAction = currentAction;
        }

        return sanitizedActions;
    }

    private ActionType ToSafeActionType(int actionValue, string ownerLabel, int slotIndex)
    {
        ActionType action = (ActionType)actionValue;

        if (Enum.IsDefined(typeof(ActionType), action))
            return action;

        Debug.LogWarning($"[TurnPlanningManager] Unknown action value {actionValue} for {ownerLabel} at slot {slotIndex + 1}; treating it as None.");
        return ActionType.None;
    }

}
