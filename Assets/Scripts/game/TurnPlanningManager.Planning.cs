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
    private ActionData hoveredAttackRangeAction;
    private int pendingLocalPlanTurn = -1;
    private int[] pendingLocalPlan;
    private bool localPlanPublished;

    private void ClearPendingLocalPlan()
    {
        pendingLocalPlanTurn = -1;
        pendingLocalPlan = null;
        localPlanPublished = false;
    }

    private bool AreBothPlayersReady(int turnIndex)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.PlayerList.Length != 2) return false;
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.IsInactive ||
                !player.CustomProperties.TryGetValue(PLAYER_PROP_READY, out object ready) || !(ready is bool isReady) || !isReady ||
                !player.CustomProperties.TryGetValue(PLAYER_PROP_SUBMIT_TURN, out object turn) || !(turn is int submittedTurn) || submittedTurn != turnIndex)
                return false;
        }
        return true;
    }

    private void TryPublishLocalPlan(int turnIndex)
    {
        if (gameEnded || !localSubmitted || localPlanPublished || pendingLocalPlan == null ||
            pendingLocalPlanTurn != turnIndex || !AreBothPlayersReady(turnIndex)) return;

        // Publish the locked snapshot, never reread editable UI after Ready.
        localPlanPublished = true;
        bool queued = PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { PLAYER_PROP_ACTIONS, (int[])pendingLocalPlan.Clone() },
            { PLAYER_PROP_ACTIONS_TURN, turnIndex }
        });
        if (!queued) localPlanPublished = false;
    }

    private bool HasPublishedPlan(Player player, int turnIndex)
    {
        return player.CustomProperties.TryGetValue(PLAYER_PROP_ACTIONS_TURN, out object turn) &&
            turn is int publishedTurn && publishedTurn == turnIndex &&
            player.CustomProperties.TryGetValue(PLAYER_PROP_ACTIONS, out object actions) &&
            (actions is int[] || actions is object[]);
    }

    private void RefreshPlanningPresentation(bool hasTurn, double remaining = 0, double duration = 1)
    {
        var phase = gameEnded ? BattleCountdownPresentation.Phase.Ended :
            !hasTurn ? BattleCountdownPresentation.Phase.Sync :
            isResolving ? BattleCountdownPresentation.Phase.Resolving :
            localSubmitted ? BattleCountdownPresentation.Phase.Waiting : BattleCountdownPresentation.Phase.Planning;
        BattleCountdownPresentation.Ensure(countdownText)?.Present(phase, remaining, duration);
        ReadyButtonPresentation.Ensure(readyButton, countdownText != null ? countdownText.font : null)?.Present(phase);
    }

    private void Update()
    {
        if (gameEnded)
        {
            RefreshPlanningPresentation(false);
            return;
        }

        if (!TryGetCurrentTurnInfo(out int turnIndex, out double turnStart, out double turnDur))
        {
            RefreshPlanningPresentation(false);
            return;
        }

        if (isResolving)
        {
            if (countdownText != null)
                countdownText.text = "__";

            RefreshPlanningPresentation(true);
            return;
        }

        double remain = GetRemainingSeconds(turnStart, turnDur);

        if (countdownText != null)
        {
            float remainFloat = Mathf.Max(0f, (float)remain);
            countdownText.text = Mathf.CeilToInt(remainFloat).ToString();
        }

        if (remain <= 0f && !localSubmitted)
        {
            SubmitLocalPlan();
        }

        RefreshPlanningPresentation(true, remain, turnDur);

        // Every client publishes its own plan only after observing both Ready markers.
        TryPublishLocalPlan(turnIndex);

        bool timeoutReadyToResolve = remain <= -timeoutResolveDelay;

        if (PhotonNetwork.IsMasterClient && !receivedResolution)
        {
            TryFinalizePlanning(turnIndex, timeoutReadyToResolve);
        }
    }

    private void OnClickReady()
    {
        if (gameEnded || localSubmitted || isResolving)
            return;

        SubmitLocalPlan();
    }

    private void BeginPlanningPhase()
    {
        if (gameEnded)
            return;

        int nextTurnIndex = 1;

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_TURN_INDEX, out object oldTurnObj))
        {
            nextTurnIndex = (int)oldTurnObj + 1;
        }

        Hashtable roomProps = new Hashtable
        {
            { ROOM_PROP_TURN_INDEX, nextTurnIndex },
            { ROOM_PROP_TURN_START, PhotonNetwork.Time },
            { ROOM_PROP_TURN_DUR, (double)planningDuration }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);

        localSubmitted = false;
        receivedResolution = false;
        isResolving = false;
        HideAllPlanningReadyIndicators();

        ResetSightSnapshotRevisionsForPlanning();
        suppressSightSlotPublishing = true;
        ResetLocalTurnProps(nextTurnIndex);
        ClearAllPlanningSlots();
        suppressSightSlotPublishing = false;
        ClearEnemyActionsPreview();
        ResetEnemyActionRevealsForPlanning();
        RestoreActionDisplayLayout();
        ResetClaimsForPlanning(nextTurnIndex);
        RefreshSightViewsForPlanningRound();
        PublishInitialSightSnapshotsForCurrentPlanningRound();

        SetPlanningInteractable(true);

        if (readyButton != null)
            readyButton.interactable = true;

        RefreshDebug("Planning phase started. Turn " + nextTurnIndex);
    }

    private void ResetLocalTurnProps(int turnIndex)
    {
        ClearPendingLocalPlan();
        Hashtable playerProps = new Hashtable
        {
            { PLAYER_PROP_READY, false },
            { PLAYER_PROP_SUBMIT_TURN, -1 },
            { PLAYER_PROP_ACTIONS, null },
            { PLAYER_PROP_ACTIONS_TURN, -1 }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(playerProps);
    }

    private void SubmitLocalPlan()
    {
        if (gameEnded || localSubmitted || isResolving || !PhotonNetwork.InRoom)
            return;

        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return;

        int[] actions = ReadLocalSlotActions();
        CaptureUsageClaims(turnIndex, actions);
        pendingLocalPlan = (int[])actions.Clone();
        pendingLocalPlanTurn = turnIndex;
        localPlanPublished = false;
        localSubmitted = true;

        Hashtable props = new Hashtable
        {
            { PLAYER_PROP_READY, true },
            { PLAYER_PROP_SUBMIT_TURN, turnIndex }
        };

        if (!PhotonNetwork.LocalPlayer.SetCustomProperties(props))
        {
            localSubmitted = false;
            ClearPendingLocalPlan();
            return;
        }
        SetPlayerReadyIndicator(PhotonNetwork.LocalPlayer, true);

        if (readyButton != null)
            readyButton.interactable = false;

        SetPlanningInteractable(false);
        HideLocalShiftReadyIndicator();
        TryPublishLocalPlan(turnIndex);
        RefreshDebug("Local plan locked; waiting for both players to be ready.");
    }

    private int[] ReadLocalSlotActions()
    {
        int slotCount = GetLocalTransmittedActionSlotCount();
        int[] actions = CreateEmptyActionArray(slotCount);

        if (planningSlots == null)
            return actions;

        int count = Mathf.Min(planningSlots.Length, actions.Length);
        for (int i = 0; i < count; i++)
        {
            if (planningSlots[i] == null)
                continue;

            DraggableItem childItem = planningSlots[i].GetComponentInChildren<DraggableItem>(true);
            if (childItem == null)
                continue;

            ActionDragData dragData = childItem.GetComponent<ActionDragData>();
            if (dragData != null)
                actions[i] = (int)dragData.actionType;
        }

        return actions;
    }

    private int GetLocalTransmittedActionSlotCount()
    {
        if (planningSlots != null && planningSlots.Length > 0)
            return planningSlots.Length;

        if (myUnit != null && myUnit.slotCount > 0)
            return myUnit.slotCount;

        CharacterClassConfig config = GetMyClassConfig();
        if (config != null && config.slotCount > 0)
            return config.slotCount;

        return Mathf.Max(1, transmittedActionSlotCount);
    }

    private int GetTransmittedActionSlotCountForPlayer(Player player)
    {
        int slotCount = GetSlotCountForPlayer(player);
        if (slotCount > 0)
            return slotCount;

        if (player == PhotonNetwork.LocalPlayer)
            return GetLocalTransmittedActionSlotCount();

        return Mathf.Max(1, transmittedActionSlotCount);
    }

    private int[] CreateEmptyActionArray(int slotCount)
    {
        int[] actions = new int[Mathf.Max(1, slotCount)];

        for (int i = 0; i < actions.Length; i++)
            actions[i] = (int)ActionType.None;

        return actions;
    }

    private int[] NormalizeTransmittedActionArray(int[] sourceActions, int slotCount)
    {
        int[] normalized = CreateEmptyActionArray(slotCount);

        if (sourceActions == null)
            return normalized;

        int count = Mathf.Min(sourceActions.Length, normalized.Length);
        for (int i = 0; i < count; i++)
            normalized[i] = sourceActions[i];

        return normalized;
    }

    private void TryFinalizePlanning(int turnIndex, bool timerExpired)
    {
        if (gameEnded)
            return;

        Player[] players = PhotonNetwork.PlayerList;
        if (players == null || players.Length < 2)
            return;

        // Timeout auto-readies each local plan in Update; it must never bypass
        // the reveal barrier or resolve a packet that has not arrived yet.
        if (!AreBothPlayersReady(turnIndex))
            return;
        foreach (Player player in players)
            if (!HasPublishedPlan(player, turnIndex)) return;

        if (receivedResolution)
            return;

        Player p1 = players[0];
        Player p2 = players[1];

        int[] p1Actions = GetPlayerActionsForTurn(p1, turnIndex);
        int[] p2Actions = GetPlayerActionsForTurn(p2, turnIndex);

        // Host sanitizes illegal consecutive Jump slots and broadcasts the same result to both clients.
        p1Actions = SanitizeConsecutiveJumpsForResolve(p1Actions, GetPlayerDisplayName(p1));
        p2Actions = SanitizeConsecutiveJumpsForResolve(p2Actions, GetPlayerDisplayName(p2));

        // Publish the resolve-start timestamp with the plans-ready event.
        int resolveStartTimestamp = GetResolveStartTimestamp();

        object[] content = new object[]
        {
        turnIndex,
        resolveStartTimestamp,
        p1.ActorNumber, p1Actions,
        p2.ActorNumber, p2Actions
        };

        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All
        };

        PhotonNetwork.RaiseEvent(EVENT_PLANS_READY, content, options, SendOptions.SendReliable);
        receivedResolution = true;
        HideAllPlanningReadyIndicators();

        RefreshDebug("Host broadcast plan resolution; waiting for shared start beat.");
    }

    public bool IsUltimateReady()
    {
        return myEnergy >= maxEnergy;
    }

    public bool HasQueuedUltimate(ActionSlot ignoreSlot = null)
    {
        if (planningSlots == null)
            return false;

        for (int i = 0; i < planningSlots.Length; i++)
        {
            ActionSlot slot = planningSlots[i];
            if (slot == null || slot == ignoreSlot)
                continue;

            ActionData data = slot.GetCurrentActionData();
            if (data is UltimateActionData)
                return true;
        }

        return false;
    }

    public bool CanDragOrPlaceAction(ActionData actionData, ActionSlot ignoreSlot = null, bool verbose = false)
    {
        if (actionData == null)
            return false;

        if (gameEnded || isResolving)
            return false;

        if (localSubmitted)
            return false;

        if (actionData.actionType == ActionType.Fade && !CanUseFade(ignoreSlot, verbose))
            return false;

        if (actionData.actionType == ActionType.Shift && !CanUseShift(ignoreSlot, verbose))
            return false;

        if (IsSightAction(actionData) && !CanUseSight(ignoreSlot, verbose))
            return false;

        if (actionData is UltimateActionData)
        {
            if (verbose)
            {
                Debug.Log($"CanDragOrPlaceAction: Ultimate unavailable, myEnergy={myEnergy}, maxEnergy={maxEnergy}, queued={HasQueuedUltimate(ignoreSlot)}");
            }

            if (!CanUseUltimate())
                return false;

            if (HasQueuedUltimate(ignoreSlot))
                return false;
        }

        return true;
    }

    public bool CanStartActionPaletteDrag(ActionData actionData)
    {
        if (actionData == null)
            return false;

        return !gameEnded && !isResolving && !localSubmitted;
    }

    public bool ShowHoveredAttackRange(ActionData actionData)
    {
        if (!CanStartActionPaletteDrag(actionData) ||
            !TryGetHoveredAttackRange(actionData, out int minRange, out int maxRange))
        {
            ClearHoveredAttackRange(actionData);
            return false;
        }

        if (myUnit == null || enemyUnit == null)
            return false;

        AttackRangePreviewManager manager = ResolveAttackRangePreviewManagerForHover();
        if (manager == null || !manager.CanShowRangeFor(myUnit))
        {
            ClearHoveredAttackRange(actionData);
            return false;
        }

        float tileSize = battleStepPlayer != null ? battleStepPlayer.WorldUnitsPerTile : WorldUnitsPerTile;
        manager.SetTileSize(tileSize);
        manager.ShowHoverRange(myUnit, enemyUnit, minRange, maxRange);
        hoveredAttackRangeAction = actionData;
        return true;
    }

    public void ClearHoveredAttackRange(ActionData actionData = null)
    {
        if (actionData != null && hoveredAttackRangeAction != null && hoveredAttackRangeAction != actionData)
            return;

        AttackRangePreviewManager manager = ResolveAttackRangePreviewManagerForHover(false);
        if (manager != null)
            manager.ClearHoverRange();

        hoveredAttackRangeAction = null;
    }

    private bool TryGetHoveredAttackRange(ActionData actionData, out int minRange, out int maxRange)
    {
        minRange = 0;
        maxRange = 0;

        if (!IsHoverRangeAttackAction(actionData))
            return false;

        if (actionData is AttackActionData attackData && attackData.range > 0)
        {
            minRange = attackData.minRange > 0 ? attackData.minRange : 1;
            maxRange = attackData.range;
            return maxRange >= minRange;
        }

        if (actionData is UltimateActionData ultimateData &&
            actionData.actionType == ActionType.Ultimate &&
            ultimateData.damage > 0 &&
            ultimateData.range > 0)
        {
            minRange = ultimateData.minRange > 0 ? ultimateData.minRange : 1;
            maxRange = ultimateData.range;
            return maxRange >= minRange;
        }

        switch (actionData.actionType)
        {
            case ActionType.LightAttack:
            case ActionType.LowAttack:
                minRange = 1;
                maxRange = 1;
                return true;

            case ActionType.HeavyAttack:
                minRange = 1;
                maxRange = 2;
                return true;

            case ActionType.Ultimate:
                minRange = 1;
                maxRange = 2;
                return true;

            case ActionType.Bolt:
                minRange = 2;
                maxRange = 4;
                return true;

            case ActionType.Rift:
                minRange = 2;
                maxRange = 3;
                return true;

            default:
                return false;
        }
    }

    private bool IsHoverRangeAttackAction(ActionData actionData)
    {
        if (actionData == null)
            return false;

        if (actionData is AttackActionData)
            return true;

        if (actionData is UltimateActionData ultimateData)
            return actionData.actionType == ActionType.Ultimate && ultimateData.damage > 0;

        switch (actionData.actionType)
        {
            case ActionType.LightAttack:
            case ActionType.HeavyAttack:
            case ActionType.LowAttack:
            case ActionType.Bolt:
            case ActionType.Rift:
                return true;

            default:
                return false;
        }
    }

    private AttackRangePreviewManager ResolveAttackRangePreviewManagerForHover(bool createIfMissing = true)
    {
        AttackRangePreviewManager manager = FindFirstObjectByType<AttackRangePreviewManager>();
        if (manager != null)
            return manager;

        if (!createIfMissing || battleStepPlayer == null)
            return null;

        manager = battleStepPlayer.GetComponent<AttackRangePreviewManager>();
        if (manager != null)
            return manager;

        return battleStepPlayer.gameObject.AddComponent<AttackRangePreviewManager>();
    }

    public bool CanUseUltimate()
    {
        return myEnergy >= maxEnergy;
    }

    public int GetSlotCostForAction(ActionData actionData, ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.HeavyAttack:
            case ActionType.Rift:
            case ActionType.Shift:
                return Mathf.Max(2, actionData != null ? actionData.GetSlotCost() : 2);

            default:
                return actionData != null ? actionData.GetSlotCost() : 1;
        }
    }

    private int GetSlotCost(ActionType action)
    {
        return GetSlotCostForAction(null, action);
    }

    private void SetPlanningInteractable(bool value)
    {
        if (readyButton != null)
            readyButton.interactable = value;

        if (planningSlots != null)
        {
            for (int i = 0; i < planningSlots.Length; i++)
            {
                if (planningSlots[i] != null)
                {
                    CanvasGroup cg = planningSlots[i].GetComponent<CanvasGroup>();
                    if (cg == null)
                        cg = planningSlots[i].gameObject.AddComponent<CanvasGroup>();

                    cg.interactable = value;
                    cg.blocksRaycasts = value;
                }
            }
        }

        ActionDragSource.GlobalDragEnabled = value;
        SetClaimInteractionEnabled(value);
    }

    private void ClearAllPlanningSlots()
    {
        if (planningSlots == null) return;

        for (int i = 0; i < planningSlots.Length; i++)
        {
            if (planningSlots[i] != null)
                planningSlots[i].ClearPlacedItemOnly();
        }
    }

}
