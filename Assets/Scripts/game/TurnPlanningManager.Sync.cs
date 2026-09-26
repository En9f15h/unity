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
    private IEnumerator WaitForSyncedGameStart()
    {
        if (gameEnded)
            yield break;

        // Wait until both scene instances reach the synchronized game start.
        while (GameSceneStartSync.Instance == null || !GameSceneStartSync.Instance.HasGameStarted())
        {
            if (gameEnded)
                yield break;

            yield return null;
        }

        while (!GameSceneStartSync.Instance.HasBeatStarted())
        {
            if (gameEnded)
                yield break;

            yield return null;
        }

        // The master publishes the shared planning start timestamp for both clients.
        if (!PhotonNetwork.IsMasterClient)
        {
            RefreshDebug("Waiting for Host to start planning.");
            planningStartCoroutine = null;
            yield break;
        }

        int planningStartTimestamp = GameSceneStartSync.Instance.GetNextBeatTimestamp(planningLeadBeats);

        RefreshDebug($"Planning start scheduled. startTs={planningStartTimestamp}");

        while (!HasReachedServerTimestamp(planningStartTimestamp))
        {
            if (gameEnded)
                yield break;

            yield return null;
        }

        BeginPlanningPhase();
        RefreshDebug("Planning started.");
        planningStartCoroutine = null;
    }

    private int[] GetPlayerActionsForTurn(Player player, int turnIndex)
    {
        int slotCount = GetTransmittedActionSlotCountForPlayer(player);

        if (player.CustomProperties.TryGetValue(PLAYER_PROP_SUBMIT_TURN, out object turnObj))
        {
            if ((int)turnObj == turnIndex &&
                player.CustomProperties.TryGetValue(PLAYER_PROP_ACTIONS, out object actionsObj))
            {
                if (actionsObj is int[] intArray)
                    return NormalizeTransmittedActionArray(intArray, slotCount);

                if (actionsObj is object[] objArray)
                {
                    int[] converted = new int[objArray.Length];
                    for (int i = 0; i < objArray.Length; i++)
                        converted[i] = Convert.ToInt32(objArray[i]);
                    return NormalizeTransmittedActionArray(converted, slotCount);
                }
            }
        }

        return CreateEmptyActionArray(slotCount);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (gameEnded)
            return;

        if (photonEvent.Code == EVENT_ORACLE_SIGHT_ACTIVATION_REQUEST)
        {
            HandleSightActivationRequestEvent(photonEvent);
            return;
        }

        if (photonEvent.Code == EVENT_ORACLE_SIGHT_ACTIVATED)
        {
            HandleSightActivatedEvent(photonEvent);
            return;
        }

        if (photonEvent.Code == EVENT_ORACLE_SIGHT_SLOT_SNAPSHOT)
        {
            HandleSightSlotSnapshotEvent(photonEvent);
            return;
        }

        if (photonEvent.Code == EVENT_CLAIM_SNAPSHOT)
        {
            HandleClaimSnapshotEvent(photonEvent);
            return;
        }

        if (photonEvent.Code != EVENT_PLANS_READY)
            return;

        object[] data = (object[])photonEvent.CustomData;

        int turnIndex = (int)data[0];
        int resolveStartTimestamp = (int)data[1];

        if (turnIndex == lastResolvedTurnIndex)
        {
            Debug.Log("Received stale plans-ready event for turnIndex = " + turnIndex);
            return;
        }

        int actorA = (int)data[2];
        int[] actionsA = (int[])data[3];
        int actorB = (int)data[4];
        int[] actionsB = (int[])data[5];

        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;

        int[] myActions = null;
        int[] enemyActions = null;

        if (actorA == myActor)
        {
            myActions = actionsA;
            enemyActions = actionsB;
        }
        else if (actorB == myActor)
        {
            myActions = actionsB;
            enemyActions = actionsA;
        }
        else
        {
            Debug.LogError("OnEvent received a malformed player ActorNumber payload.");
            return;
        }

        // The accepted plans-ready event is the shared confirmation point, not the local Ready click.
        RecordConfirmedUsageClaims(turnIndex);

        // Validate again on the resolve side to prevent legacy clients, inspector edits, or bad packets from chaining Jump.
        myActions = SanitizeConsecutiveJumpsForResolve(myActions, "My");
        enemyActions = SanitizeConsecutiveJumpsForResolve(enemyActions, "Enemy");

        lastResolvedTurnIndex = turnIndex;

        ApplyActionDisplayLayout();
        PrepareEnemyActionsPreviewForResolution();

        if (resolveStartCoroutine != null)
        {
            StopCoroutine(resolveStartCoroutine);
            resolveStartCoroutine = null;
        }

        if (resolveCoroutine != null)
        {
            StopCoroutine(resolveCoroutine);
            resolveCoroutine = null;
        }

        // Start resolve at the synchronized timestamp carried by the event payload.
        HideAllPlanningReadyIndicators();
        resolveStartCoroutine = StartCoroutine(
            WaitForResolveBeatThenStart(turnIndex, resolveStartTimestamp, myActions, enemyActions)
        );
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(ROOM_PROP_GAME_ENDED) ||
            propertiesThatChanged.ContainsKey(ROOM_PROP_GAME_WINNER_ACTOR))
        {
            ApplyGameResultFromRoom();
        }

        if (ContainsSightRoomProperty(propertiesThatChanged))
        {
            SyncSightEnergyLocksFromSightState();
            NotifyKnownSightStatesChanged();
            UpdateEnergyBars();
            RefreshSightViewsForPlanningRound();
        }

        if (gameEnded)
            return;

        if (propertiesThatChanged.ContainsKey(ROOM_PROP_TURN_INDEX))
        {
            localSubmitted = false;
            receivedResolution = false;
            isResolving = false;
            HideAllPlanningReadyIndicators();

            ResetSightSnapshotRevisionsForPlanning();
            suppressSightSlotPublishing = true;
            ClearAllPlanningSlots();
            suppressSightSlotPublishing = false;
            ClearEnemyActionsPreview();
            ResetEnemyActionRevealsForPlanning();
            RestoreActionDisplayLayout();
            int claimRound = 0;
            if (TryGetCurrentTurnInfo(out int claimTurnIndex, out double claimTurnStart, out double claimTurnDuration))
                claimRound = claimTurnIndex;

            ResetClaimsForPlanning(claimRound);
            RefreshSightViewsForPlanningRound();
            PublishInitialSightSnapshotsForCurrentPlanningRound();

            if (readyButton != null)
                readyButton.interactable = true;

            SetPlanningInteractable(true);
            RefreshDebug("Both plans received; showing enemy slots.");
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (gameEnded)
            return;

        if (changedProps.ContainsKey(PLAYER_PROP_READY) ||
            changedProps.ContainsKey(PLAYER_PROP_ACTIONS) ||
            changedProps.ContainsKey(PLAYER_PROP_SUBMIT_TURN))
            {
                RefreshPlanningReadyIndicatorsFromPhoton();
                RefreshDebug("Player properties updated: " + GetPlayerDisplayName(targetPlayer));
                TryShowSightRevealForPlayer(targetPlayer);
            }
    }

    private string GetPlayerDisplayName(Player player)
    {
        if (player == null)
            return "P?";

        return player.IsMasterClient ? "P1" : "P2";
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (gameEnded)
            return;

        int localActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;
        FinishGame(localActorNumber, "OpponentLeft", true);
    }

}
