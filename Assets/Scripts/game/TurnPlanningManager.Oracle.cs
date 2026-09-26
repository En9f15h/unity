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
    private struct OracleSightState
    {
        public int ownerActorNumber;
        public int targetActorNumber;
        public int watchedLogicalSlotIndex;
        public int activationRound;
        public bool used;
        public bool active;
    }

    public bool IsActionOnCooldown(ActionData actionData)
    {
        if (actionData == null)
            return false;

        if (actionData.actionType != ActionType.Fade &&
            actionData.actionType != ActionType.Shift)
            return false;

        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return false;

        if (actionData.actionType == ActionType.Fade)
            return myLastFadeTurn == turnIndex - 1;

        return myLastShiftTurn == turnIndex - 1;
    }

    public bool IsActionUsed(ActionData actionData)
    {
        if (actionData == null)
            return false;

        if (actionData.actionType == ActionType.Sight)
            return IsSightUsedForActor(PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber);

        if (actionData.actionType == ActionType.Fade && TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return myLastFadeTurn == turnIndex;

        if (actionData.actionType == ActionType.Shift && TryGetCurrentTurnInfo(out int shiftTurnIndex, out _, out _))
            return myLastShiftTurn == shiftTurnIndex;

        return false;
    }

    private bool IsOracleClass(bool isMine)
    {
        CharacterClassConfig config = isMine ? GetMyClassConfig() : GetEnemyClassConfig();
        return IsOracleConfig(config);
    }

    private bool IsOracleClassForPlayer(Player player)
    {
        if (player == null)
            return false;

        return IsOracleConfig(GetClassConfigByIndex(GetPlayerClassIndex(player)));
    }

    private bool IsOracleConfig(CharacterClassConfig config)
    {
        if (config == null)
            return false;

        return config is OracleClassConfig || config.className == "Oracle";
    }

    private bool IsOracleAction(ActionType action)
    {
        return action == ActionType.Bolt ||
               action == ActionType.Rift ||
               action == ActionType.Shift ||
               action == ActionType.Ward ||
               action == ActionType.Fade ||
               action == ActionType.Sight;
    }

    private bool IsSightAction(ActionData actionData)
    {
        return actionData != null && actionData.actionType == ActionType.Sight;
    }

    private bool IsSightAction(ActionType action)
    {
        return action == ActionType.Sight;
    }

    private bool HasQueuedAction(ActionType actionType, ActionSlot ignoreSlot = null)
    {
        if (planningSlots == null)
            return false;

        for (int i = 0; i < planningSlots.Length; i++)
        {
            ActionSlot slot = planningSlots[i];
            if (slot == null || slot == ignoreSlot)
                continue;

            if (slot.GetCurrentActionType() == actionType)
                return true;
        }

        return false;
    }

    private bool CanUseFade(ActionSlot ignoreSlot, bool verbose)
    {
        if (HasQueuedAction(ActionType.Fade, ignoreSlot))
        {
            if (verbose)
                Debug.Log("[OracleSight] Fade is already queued this round.");

            return false;
        }

        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return true;

        if (myLastFadeTurn == turnIndex)
        {
            if (verbose)
                Debug.Log("[OracleSight] Fade was already used this round.");

            return false;
        }

        if (myLastFadeTurn == turnIndex - 1)
        {
            if (verbose)
                Debug.Log("[OracleSight] Fade is on cooldown from the previous round.");

            return false;
        }

        return true;
    }

    private bool CanUseShift(ActionSlot ignoreSlot, bool verbose)
    {
        if (HasQueuedAction(ActionType.Shift, ignoreSlot))
        {
            if (verbose)
                Debug.Log("[OracleShift] Shift is already queued this round.");

            return false;
        }

        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return true;

        if (myLastShiftTurn == turnIndex)
        {
            if (verbose)
                Debug.Log("[OracleShift] Shift was already used this round.");

            return false;
        }

        if (myLastShiftTurn == turnIndex - 1)
        {
            if (verbose)
                Debug.Log("[OracleShift] Shift is on cooldown from the previous round.");

            return false;
        }

        return true;
    }

    private bool CanUseSight(ActionSlot ignoreSlot, bool verbose)
    {
        int actorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;

        if (!IsOracleClass(true))
        {
            if (verbose)
                Debug.Log("[OracleSight] Sight is unavailable because the local character is not Oracle.");

            return false;
        }

        if (IsSightUsedForActor(actorNumber))
        {
            if (verbose)
                Debug.Log("[OracleSight] Sight has already been used this match.");

            return false;
        }

        if (!CanUseUltimate())
        {
            if (verbose)
                Debug.Log("[OracleSight] Sight is unavailable because energy is not full.");

            return false;
        }

        if (HasQueuedAction(ActionType.Sight, ignoreSlot))
        {
            if (verbose)
                Debug.Log("[OracleSight] Sight is already queued.");

            return false;
        }

        return true;
    }

    private string GetSightUsedKey(int actorNumber)
    {
        return ROOM_PROP_SIGHT_PREFIX + actorNumber + "_Used";
    }

    private string GetSightActiveKey(int actorNumber)
    {
        return ROOM_PROP_SIGHT_PREFIX + actorNumber + "_Active";
    }

    private string GetSightTargetKey(int actorNumber)
    {
        return ROOM_PROP_SIGHT_PREFIX + actorNumber + "_Target";
    }

    private string GetSightSlotKey(int actorNumber)
    {
        return ROOM_PROP_SIGHT_PREFIX + actorNumber + "_Slot";
    }

    private string GetSightRoundKey(int actorNumber)
    {
        return ROOM_PROP_SIGHT_PREFIX + actorNumber + "_Round";
    }

    private bool TryGetSightStateForOwner(int ownerActorNumber, out OracleSightState state)
    {
        state = new OracleSightState
        {
            ownerActorNumber = ownerActorNumber,
            targetActorNumber = -1,
            watchedLogicalSlotIndex = -1,
            activationRound = -1,
            used = false,
            active = false
        };

        if (ownerActorNumber <= 0 || PhotonNetwork.CurrentRoom == null)
            return false;

        Hashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (props == null)
            return false;

        state.used = TryReadBoolRoomProp(props, GetSightUsedKey(ownerActorNumber));
        state.active = TryReadBoolRoomProp(props, GetSightActiveKey(ownerActorNumber));
        state.targetActorNumber = TryReadIntRoomProp(props, GetSightTargetKey(ownerActorNumber), -1);
        state.watchedLogicalSlotIndex = TryReadIntRoomProp(props, GetSightSlotKey(ownerActorNumber), -1);
        state.activationRound = TryReadIntRoomProp(props, GetSightRoundKey(ownerActorNumber), -1);

        return state.used || state.active || state.watchedLogicalSlotIndex >= 0;
    }

    private bool TryReadBoolRoomProp(Hashtable props, string key)
    {
        return props != null &&
               props.TryGetValue(key, out object value) &&
               value != null &&
               Convert.ToBoolean(value);
    }

    private int TryReadIntRoomProp(Hashtable props, string key, int fallback)
    {
        if (props == null || !props.TryGetValue(key, out object value) || value == null)
            return fallback;

        try
        {
            return Convert.ToInt32(value);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private bool IsSightUsedForActor(int actorNumber)
    {
        return TryGetSightStateForOwner(actorNumber, out OracleSightState state) && state.used;
    }

    private bool IsSightActivatedForActor(int actorNumber)
    {
        return TryGetSightStateForOwner(actorNumber, out OracleSightState state) && state.active;
    }

    private bool TryGetSightSlotForActor(int actorNumber, out int slotIndex)
    {
        slotIndex = -1;

        if (!TryGetSightStateForOwner(actorNumber, out OracleSightState state))
            return false;

        slotIndex = state.watchedLogicalSlotIndex;
        return slotIndex >= 0;
    }

    private bool IsSightActiveForActor(int actorNumber, int turnIndex)
    {
        if (!TryGetSightStateForOwner(actorNumber, out OracleSightState state))
            return false;

        return IsSightRevealVisible(state, turnIndex);
    }

    private bool IsSightRevealVisible(OracleSightState state, int turnIndex)
    {
        return state.active &&
               state.used &&
               state.targetActorNumber > 0 &&
               state.watchedLogicalSlotIndex >= 0 &&
               state.activationRound >= 0 &&
               turnIndex > state.activationRound;
    }

    private bool IsSightWatchedPresentationVisible(OracleSightState state)
    {
        return state.active &&
               state.used &&
               state.targetActorNumber > 0 &&
               state.watchedLogicalSlotIndex >= 0;
    }

    private void RequestSightActivation(int ownerActorNumber, int targetActorNumber, int activationRound)
    {
        if (ownerActorNumber <= 0 || targetActorNumber <= 0)
        {
            Debug.LogWarning("[OracleSight] Activation request skipped because owner or target actor is invalid.");
            return;
        }

        Debug.Log($"[OracleSight] Activation request sent. Owner={ownerActorNumber} Target={targetActorNumber} Round={activationRound}");

        if (PhotonNetwork.IsMasterClient)
        {
            HandleSightActivationRequest(ownerActorNumber, targetActorNumber, activationRound, ownerActorNumber);
            return;
        }

        Player master = PhotonNetwork.MasterClient;
        if (master == null)
        {
            Debug.LogWarning("[OracleSight] Activation request failed because MasterClient is missing.");
            return;
        }

        object[] payload = new object[]
        {
            ownerActorNumber,
            targetActorNumber,
            activationRound
        };

        RaiseEventOptions options = new RaiseEventOptions
        {
            TargetActors = new[] { master.ActorNumber }
        };

        PhotonNetwork.RaiseEvent(EVENT_ORACLE_SIGHT_ACTIVATION_REQUEST, payload, options, SendOptions.SendReliable);
    }

    private void HandleSightActivationRequestEvent(EventData photonEvent)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!TryReadSightActivationPayload(photonEvent.CustomData, out int ownerActorNumber, out int targetActorNumber, out int activationRound))
        {
            Debug.LogWarning("[OracleSight] Activation request rejected. Reason=MalformedPayload");
            return;
        }

        HandleSightActivationRequest(ownerActorNumber, targetActorNumber, activationRound, photonEvent.Sender);
    }

    private bool TryReadSightActivationPayload(object payload, out int ownerActorNumber, out int targetActorNumber, out int activationRound)
    {
        ownerActorNumber = -1;
        targetActorNumber = -1;
        activationRound = -1;

        if (!(payload is object[] data) || data.Length < 3)
            return false;

        try
        {
            ownerActorNumber = Convert.ToInt32(data[0]);
            targetActorNumber = Convert.ToInt32(data[1]);
            activationRound = Convert.ToInt32(data[2]);
            return true;
        }
        catch (Exception)
        {
            ownerActorNumber = -1;
            targetActorNumber = -1;
            activationRound = -1;
            return false;
        }
    }

    private void HandleSightActivationRequest(int ownerActorNumber, int targetActorNumber, int activationRound, int senderActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        if (senderActorNumber != ownerActorNumber)
        {
            Debug.LogWarning($"[OracleSight] Activation request rejected. Reason=SenderMismatch Sender={senderActorNumber} Owner={ownerActorNumber}");
            return;
        }

        Player owner = GetPlayerByActorNumber(ownerActorNumber);
        Player target = GetPlayerByActorNumber(targetActorNumber);

        if (owner == null || target == null || owner == target)
        {
            Debug.LogWarning("[OracleSight] Activation request rejected. Reason=InvalidOwnerOrTarget");
            return;
        }

        if (!IsOracleClassForPlayer(owner))
        {
            Debug.LogWarning($"[OracleSight] Activation request rejected. Reason=OwnerIsNotOracle Owner={ownerActorNumber}");
            return;
        }

        if (IsSightUsedForActor(ownerActorNumber))
        {
            Debug.LogWarning($"[OracleSight] Activation request rejected. Reason=AlreadyUsed Owner={ownerActorNumber}");
            return;
        }

        if (!PlayerSubmittedSightForRound(owner, activationRound))
        {
            Debug.LogWarning($"[OracleSight] Activation request rejected. Reason=SightNotSubmitted Owner={ownerActorNumber} Round={activationRound}");
            return;
        }

        int targetSlotCount = GetSlotCountForPlayer(target);
        if (targetSlotCount <= 0)
        {
            Debug.LogWarning($"[OracleSight] Activation request rejected. Reason=TargetHasNoSlots Target={targetActorNumber}");
            return;
        }

        int watchedLogicalSlotIndex = UnityEngine.Random.Range(0, targetSlotCount);
        ConfirmSightActivation(ownerActorNumber, targetActorNumber, watchedLogicalSlotIndex, activationRound);
    }

    private bool PlayerSubmittedSightForRound(Player owner, int activationRound)
    {
        if (owner == null || activationRound <= 0)
            return false;

        int[] actions = GetPlayerActionsForTurn(owner, activationRound);
        if (actions == null)
            return false;

        for (int i = 0; i < actions.Length; i++)
        {
            if (ToSafeActionType(actions[i], "SightValidation", i) == ActionType.Sight)
                return true;
        }

        return false;
    }

    private void ConfirmSightActivation(int ownerActorNumber, int targetActorNumber, int watchedLogicalSlotIndex, int activationRound)
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable
        {
            { GetSightUsedKey(ownerActorNumber), true },
            { GetSightActiveKey(ownerActorNumber), true },
            { GetSightTargetKey(ownerActorNumber), targetActorNumber },
            { GetSightSlotKey(ownerActorNumber), watchedLogicalSlotIndex },
            { GetSightRoundKey(ownerActorNumber), activationRound }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        object[] payload = new object[]
        {
            ownerActorNumber,
            targetActorNumber,
            watchedLogicalSlotIndex,
            activationRound
        };

        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All
        };

        PhotonNetwork.RaiseEvent(EVENT_ORACLE_SIGHT_ACTIVATED, payload, options, SendOptions.SendReliable);
        ApplySightEnergyLockForActor(ownerActorNumber);
        NotifySightStateChanged(ownerActorNumber);
        Debug.Log($"[OracleSight] Activation confirmed. Owner={ownerActorNumber} Target={targetActorNumber} LogicalSlot={watchedLogicalSlotIndex}");
    }

    private void HandleSightActivatedEvent(EventData photonEvent)
    {
        if (!TryReadSightActivatedPayload(photonEvent.CustomData, out int ownerActorNumber, out int targetActorNumber, out int watchedLogicalSlotIndex, out int activationRound))
        {
            Debug.LogWarning("[OracleSight] Activation confirmation ignored. Reason=MalformedPayload");
            return;
        }

        Debug.Log($"[OracleSight] Activation received. Owner={ownerActorNumber} Target={targetActorNumber} LogicalSlot={watchedLogicalSlotIndex} Round={activationRound}");
        ApplySightEnergyLockForActor(ownerActorNumber);
        NotifySightStateChanged(ownerActorNumber);
        UpdateEnergyBars();
        RefreshSightViewsForPlanningRound();
    }

    private bool TryReadSightActivatedPayload(object payload, out int ownerActorNumber, out int targetActorNumber, out int watchedLogicalSlotIndex, out int activationRound)
    {
        ownerActorNumber = -1;
        targetActorNumber = -1;
        watchedLogicalSlotIndex = -1;
        activationRound = -1;

        if (!(payload is object[] data) || data.Length < 4)
            return false;

        try
        {
            ownerActorNumber = Convert.ToInt32(data[0]);
            targetActorNumber = Convert.ToInt32(data[1]);
            watchedLogicalSlotIndex = Convert.ToInt32(data[2]);
            activationRound = Convert.ToInt32(data[3]);
            return true;
        }
        catch (Exception)
        {
            ownerActorNumber = -1;
            targetActorNumber = -1;
            watchedLogicalSlotIndex = -1;
            activationRound = -1;
            return false;
        }
    }

    private Player GetPlayerByActorNumber(int actorNumber)
    {
        Player[] players = PhotonNetwork.PlayerList;
        if (players == null)
            return null;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].ActorNumber == actorNumber)
                return players[i];
        }

        return null;
    }

    private int GetOpponentActorNumber(int actorNumber)
    {
        Player[] players = PhotonNetwork.PlayerList;
        if (players == null)
            return -1;

        for (int i = 0; i < players.Length; i++)
        {
            Player player = players[i];
            if (player != null && player.ActorNumber != actorNumber)
                return player.ActorNumber;
        }

        return -1;
    }

    private int GetOpponentSlotCountForActor(int actorNumber)
    {
        int opponentActorNumber = GetOpponentActorNumber(actorNumber);
        Player opponent = GetPlayerByActorNumber(opponentActorNumber);
        return GetSlotCountForPlayer(opponent);
    }

    private int GetSlotCountForPlayer(Player player)
    {
        if (player == null)
            return 0;

        CharacterClassConfig config = GetClassConfigByIndex(GetPlayerClassIndex(player));
        return config != null ? config.slotCount : 0;
    }

    public void NotifyLocalPlanningSlotsChanged(ActionSlot primarySlot, params ActionSlot[] relatedSlots)
    {
        if (suppressSightSlotPublishing || gameEnded)
            return;

        HashSet<int> changedLogicalIndices = new HashSet<int>();
        AddChangedSlotIndex(changedLogicalIndices, primarySlot);

        if (relatedSlots != null)
        {
            for (int i = 0; i < relatedSlots.Length; i++)
                AddChangedSlotIndex(changedLogicalIndices, relatedSlots[i]);
        }

        if (changedLogicalIndices.Count == 0)
            return;

        PublishSightSnapshotsForChangedSlots(changedLogicalIndices);
        RefreshLocalWatchedSlots();
        RefreshLocalShiftReadyIndicator();
    }

    private void AddChangedSlotIndex(HashSet<int> changedLogicalIndices, ActionSlot slot)
    {
        if (changedLogicalIndices == null || slot == null)
            return;

        changedLogicalIndices.Add(slot.slotIndex);
    }

    private void PublishSightSnapshotsForChangedSlots(HashSet<int> changedLogicalIndices)
    {
        if (changedLogicalIndices == null || changedLogicalIndices.Count == 0)
            return;

        if (isResolving || localSubmitted)
            return;

        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return;

        int localActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;
        if (localActorNumber <= 0)
            return;

        foreach (OracleSightState state in EnumerateSightStates())
        {
            if (state.targetActorNumber != localActorNumber)
                continue;

            if (!IsSightWatchedPresentationVisible(state))
                continue;

            if (!changedLogicalIndices.Contains(state.watchedLogicalSlotIndex))
                continue;

            PublishSightSnapshotForState(state, turnIndex);
        }
    }

    private void PublishInitialSightSnapshotsForCurrentPlanningRound()
    {
        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return;

        int localActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;
        if (localActorNumber <= 0)
            return;

        foreach (OracleSightState state in EnumerateSightStates())
        {
            if (state.targetActorNumber != localActorNumber)
                continue;

            if (!IsSightWatchedPresentationVisible(state))
                continue;

            PublishSightSnapshotForState(state, turnIndex);
        }
    }

    private IEnumerable<OracleSightState> EnumerateSightStates()
    {
        Player[] players = PhotonNetwork.PlayerList;
        if (players == null)
            yield break;

        for (int i = 0; i < players.Length; i++)
        {
            Player player = players[i];
            if (player == null)
                continue;

            if (TryGetSightStateForOwner(player.ActorNumber, out OracleSightState state))
                yield return state;
        }
    }

    private void PublishSightSnapshotForState(OracleSightState state, int planningRound)
    {
        if (state.ownerActorNumber <= 0 || state.targetActorNumber <= 0)
            return;

        int revision = GetNextLocalSightRevision(state.ownerActorNumber);
        SlotSnapshot snapshot = BuildLocalSlotSnapshot(state.watchedLogicalSlotIndex, planningRound, revision);
        SendSightSlotSnapshot(state.ownerActorNumber, snapshot);
    }

    private int GetNextLocalSightRevision(int sightOwnerActorNumber)
    {
        if (!sightLocalSnapshotRevisions.TryGetValue(sightOwnerActorNumber, out int revision))
            revision = 0;

        revision++;
        sightLocalSnapshotRevisions[sightOwnerActorNumber] = revision;
        return revision;
    }

    private SlotSnapshot BuildLocalSlotSnapshot(int logicalSlotIndex, int planningRound, int revision)
    {
        SlotSnapshot snapshot = new SlotSnapshot
        {
            logicalSlotIndex = logicalSlotIndex,
            actionId = (int)ActionType.None,
            isEmpty = true,
            isLockedContinuation = false,
            sourceLogicalSlotIndex = -1,
            ownerActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber,
            planningRound = planningRound,
            revision = revision
        };

        ActionSlot slot = GetLocalPlanningSlotByLogicalIndex(logicalSlotIndex);
        if (slot == null)
            return snapshot;

        if (slot.IsOccupiedByHeavyExtension)
        {
            snapshot.isEmpty = false;
            snapshot.isLockedContinuation = true;

            if (TryGetLocalContinuationSource(logicalSlotIndex, out int sourceLogicalIndex, out ActionType sourceAction))
            {
                snapshot.sourceLogicalSlotIndex = sourceLogicalIndex;
                snapshot.actionId = (int)sourceAction;
            }

            return snapshot;
        }

        ActionType action = slot.GetCurrentActionType();
        if (action == ActionType.None || slot.GetCurrentActionData() == null)
            return snapshot;

        snapshot.actionId = (int)action;
        snapshot.isEmpty = false;
        return snapshot;
    }

    private ActionSlot GetLocalPlanningSlotByLogicalIndex(int logicalSlotIndex)
    {
        if (planningSlots == null)
            return null;

        for (int i = 0; i < planningSlots.Length; i++)
        {
            if (planningSlots[i] != null && planningSlots[i].slotIndex == logicalSlotIndex)
                return planningSlots[i];
        }

        return null;
    }

    private bool TryGetLocalContinuationSource(int logicalSlotIndex, out int sourceLogicalIndex, out ActionType sourceAction)
    {
        sourceLogicalIndex = -1;
        sourceAction = ActionType.None;

        if (planningSlots == null || logicalSlotIndex <= 0)
            return false;

        for (int i = 0; i < planningSlots.Length; i++)
        {
            ActionSlot slot = planningSlots[i];
            if (slot == null || slot.slotIndex >= logicalSlotIndex)
                continue;

            ActionType action = slot.GetCurrentActionType();
            if (action == ActionType.None)
                continue;

            int cost = GetSlotCostForAction(slot.GetCurrentActionData(), action);
            if (cost > 1 && slot.slotIndex + cost > logicalSlotIndex)
            {
                sourceLogicalIndex = slot.slotIndex;
                sourceAction = action;
                return true;
            }
        }

        return false;
    }

    private void SendSightSlotSnapshot(int sightOwnerActorNumber, SlotSnapshot snapshot)
    {
        if (sightOwnerActorNumber <= 0)
            return;

        RaiseEventOptions options = new RaiseEventOptions
        {
            TargetActors = new[] { sightOwnerActorNumber }
        };

        PhotonNetwork.RaiseEvent(
            EVENT_ORACLE_SIGHT_SLOT_SNAPSHOT,
            snapshot.ToPayload(sightOwnerActorNumber),
            options,
            SendOptions.SendReliable
        );

        if (verboseSightLogs)
        {
            ActionType action = (ActionType)snapshot.actionId;
            Debug.Log($"[OracleSight] Slot update sent. Slot={snapshot.logicalSlotIndex} Action={action} Round={snapshot.planningRound} Revision={snapshot.revision}");
        }
    }

    private void HandleSightSlotSnapshotEvent(EventData photonEvent)
    {
        if (!SlotSnapshot.TryFromPayload(photonEvent.CustomData, out int sightOwnerActorNumber, out SlotSnapshot snapshot))
        {
            Debug.LogWarning("[OracleSight] Slot snapshot ignored. Reason=MalformedPayload");
            return;
        }

        int localActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;
        if (sightOwnerActorNumber != localActorNumber)
            return;

        if (!TryGetSightStateForOwner(sightOwnerActorNumber, out OracleSightState state))
        {
            Debug.LogWarning("[OracleSight] Slot snapshot ignored. Reason=MissingSightState");
            return;
        }

        if (!ValidateSightSnapshot(state, snapshot))
            return;

        sightAcceptedSnapshotRevisions[sightOwnerActorNumber] = snapshot.revision;
        ApplySightSnapshotToEnemyPreview(state, snapshot);

        if (verboseSightLogs)
        {
            ActionType action = (ActionType)snapshot.actionId;
            Debug.Log($"[OracleSight] Slot update accepted. Slot={snapshot.logicalSlotIndex} Action={action} Round={snapshot.planningRound} Revision={snapshot.revision}");
        }
    }

    private bool ValidateSightSnapshot(OracleSightState state, SlotSnapshot snapshot)
    {
        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return false;

        if (!IsSightRevealVisible(state, turnIndex))
            return false;

        if (snapshot.ownerActorNumber != state.targetActorNumber)
        {
            Debug.LogWarning($"[OracleSight] Slot snapshot ignored. Reason=OwnerMismatch SnapshotOwner={snapshot.ownerActorNumber} Expected={state.targetActorNumber}");
            return false;
        }

        if (snapshot.logicalSlotIndex != state.watchedLogicalSlotIndex)
        {
            Debug.LogWarning($"[OracleSight] Slot snapshot ignored. Reason=SlotMismatch SnapshotSlot={snapshot.logicalSlotIndex} Expected={state.watchedLogicalSlotIndex}");
            return false;
        }

        if (snapshot.planningRound < turnIndex)
        {
            Debug.Log($"[OracleSight] Ignored stale snapshot. ReceivedRound={snapshot.planningRound} CurrentRound={turnIndex}");
            return false;
        }

        if (sightAcceptedSnapshotRevisions.TryGetValue(state.ownerActorNumber, out int currentRevision) &&
            snapshot.revision <= currentRevision)
        {
            Debug.Log($"[OracleSight] Ignored stale snapshot. ReceivedRevision={snapshot.revision} CurrentRevision={currentRevision}");
            return false;
        }

        return true;
    }

    private void ApplySightSnapshotToEnemyPreview(OracleSightState state, SlotSnapshot snapshot)
    {
        if (currentEnemyPreviewImages == null || currentEnemyPreviewImages.Count == 0)
            return;

        int visualIndex = LogicalToVisualSlotIndex(
            snapshot.logicalSlotIndex,
            currentEnemyPreviewImages.Count,
            mirrorEnemyPreviewSlotOrder
        );

        if (visualIndex < 0 || visualIndex >= currentEnemyPreviewImages.Count)
            return;

        Image target = currentEnemyPreviewImages[visualIndex];
        Sprite sprite = GetSpriteForSnapshot(snapshot);

        if (target != null)
        {
            target.sprite = sprite != null ? sprite : emptyPreviewSprite;
            target.color = snapshot.isLockedContinuation
                ? new Color(1f, 1f, 1f, 0.55f)
                : Color.white;
        }

        SightSlotView view = visualIndex < currentEnemyPreviewSightViews.Count ? currentEnemyPreviewSightViews[visualIndex] : null;
        if (view != null)
            view.ShowSnapshot(snapshot, sprite, emptyPreviewSprite);
    }

    private Sprite GetSpriteForSnapshot(SlotSnapshot snapshot)
    {
        if (snapshot.isEmpty || snapshot.actionId == (int)ActionType.None)
            return emptyPreviewSprite;

        ActionType action = ToSafeActionType(snapshot.actionId, "SightSnapshot", snapshot.logicalSlotIndex);
        action = GetEnemyPreviewDisplayAction(action);

        if (snapshot.isLockedContinuation)
        {
            CharacterClassConfig config = GetEnemyClassConfig();
            ActionData actionData = config != null ? config.GetActionData(action) : null;
            if (actionData != null)
            {
                if (actionData.lockedContinuationSprite != null)
                    return actionData.lockedContinuationSprite;

                if (actionData.iconSprite != null)
                    return actionData.iconSprite;
            }
        }

        return GetPreviewSprite(action);
    }

    private void ResetSightSnapshotRevisionsForPlanning()
    {
        sightAcceptedSnapshotRevisions.Clear();
        sightLocalSnapshotRevisions.Clear();
    }

    private void RefreshSightRevealFrame()
    {
        RefreshSightViewsForPlanningRound();
    }

    private void RefreshSightViewsForPlanningRound()
    {
        ResetAllSightSlotViews(false);

        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return;

        RefreshEnemyPreviewSightSlots(turnIndex);
        RefreshLocalWatchedSlots(turnIndex);
    }

    private void RefreshEnemyPreviewSightSlots(int turnIndex)
    {
        int localActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;
        if (localActorNumber <= 0 || currentEnemyPreviewSightViews == null)
            return;

        foreach (OracleSightState state in EnumerateSightStates())
        {
            if (state.ownerActorNumber != localActorNumber)
                continue;

            if (!IsSightRevealVisible(state, turnIndex))
                continue;

            int visualIndex = LogicalToVisualSlotIndex(
                state.watchedLogicalSlotIndex,
                currentEnemyPreviewSightViews.Count,
                mirrorEnemyPreviewSlotOrder
            );

            if (visualIndex >= 0 && visualIndex < currentEnemyPreviewSightViews.Count && currentEnemyPreviewSightViews[visualIndex] != null)
                currentEnemyPreviewSightViews[visualIndex].SetWatched(true);
        }
    }

    private void RefreshLocalWatchedSlots()
    {
        if (TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            RefreshLocalWatchedSlots(turnIndex);
    }

    private void RefreshLocalWatchedSlots(int turnIndex)
    {
        int localActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;
        if (localActorNumber <= 0 || planningSlots == null)
            return;

        for (int i = 0; i < planningSlots.Length; i++)
        {
            SightSlotView view = planningSlots[i] != null ? planningSlots[i].GetComponent<SightSlotView>() : null;
            if (view != null)
                view.SetWatched(false);
        }

        foreach (OracleSightState state in EnumerateSightStates())
        {
            if (state.targetActorNumber != localActorNumber)
                continue;

            if (!IsSightRevealVisible(state, turnIndex))
                continue;

            int visualIndex = LogicalToVisualSlotIndex(
                state.watchedLogicalSlotIndex,
                planningSlots.Length,
                mirrorLocalPlanningSlotOrder
            );

            if (visualIndex < 0 || visualIndex >= planningSlots.Length || planningSlots[visualIndex] == null)
                continue;

            SightSlotView view = planningSlots[visualIndex].GetComponent<SightSlotView>();
            if (view != null)
                view.SetWatched(true);
        }
    }

    private void ResetAllSightSlotViews(bool clearReveal)
    {
        if (currentEnemyPreviewSightViews != null)
        {
            for (int i = 0; i < currentEnemyPreviewSightViews.Count; i++)
            {
                if (currentEnemyPreviewSightViews[i] == null)
                    continue;

                currentEnemyPreviewSightViews[i].SetWatched(false);

                if (clearReveal)
                    currentEnemyPreviewSightViews[i].ClearReveal();
            }
        }

        if (planningSlots != null)
        {
            for (int i = 0; i < planningSlots.Length; i++)
            {
                if (planningSlots[i] == null)
                    continue;

                SightSlotView view = planningSlots[i].GetComponent<SightSlotView>();
                if (view != null)
                {
                    view.SetWatched(false);

                    if (clearReveal)
                        view.ClearReveal();
                }
            }
        }
    }

    private void RegisterLocalSightSlotViews()
    {
        if (planningSlots == null)
            return;

        for (int i = 0; i < planningSlots.Length; i++)
        {
            if (planningSlots[i] == null)
                continue;

            SightSlotView view = planningSlots[i].GetComponent<SightSlotView>();
            if (view == null)
                view = planningSlots[i].gameObject.AddComponent<SightSlotView>();

            ConfigureSightSlotView(view);
            view.SetWatchedSpriteTarget(planningSlots[i].GetComponent<Image>());
        }
    }

    private SightSlotView GetOrCreateSightSlotView(GameObject target)
    {
        if (target == null)
            return null;

        SightSlotView view = target.GetComponent<SightSlotView>();
        if (view == null)
            view = target.AddComponent<SightSlotView>();

        ConfigureSightSlotView(view);
        return view;
    }

    private void ConfigureSightSlotView(SightSlotView view)
    {
        if (view == null)
            return;

        view.ResolveReferences();

        Sprite watchedSlotSprite = ResolveSightWatchedSlotSprite();
        if (watchedSlotSprite != null)
            view.SetWatchedSlotSprite(watchedSlotSprite);
    }

    private Sprite ResolveSightWatchedSlotSprite()
    {
        Sprite sprite = ResolveSightWatchedSlotSprite(GetMyClassConfig());
        if (sprite != null)
            return sprite;

        sprite = ResolveSightWatchedSlotSprite(GetEnemyClassConfig());
        if (sprite != null)
            return sprite;

        if (classConfigs == null)
            return null;

        for (int i = 0; i < classConfigs.Length; i++)
        {
            sprite = ResolveSightWatchedSlotSprite(classConfigs[i]);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private Sprite ResolveSightWatchedSlotSprite(CharacterClassConfig config)
    {
        OracleClassConfig oracleConfig = config as OracleClassConfig;
        return oracleConfig != null ? oracleConfig.sightSelectedSlotFrame : null;
    }

    private int LogicalToVisualSlotIndex(int logicalIndex, int slotCount, bool isMirrored)
    {
        if (slotCount <= 0 || logicalIndex < 0 || logicalIndex >= slotCount)
            return -1;

        return isMirrored ? slotCount - 1 - logicalIndex : logicalIndex;
    }

    private int GetSafeSlotIndex(ActionSlot slot)
    {
        return slot != null ? slot.slotIndex : int.MaxValue;
    }

    private bool TryGetLocalSightRevealSlot(out int slotIndex)
    {
        slotIndex = -1;

        if (!IsOracleClass(true))
            return false;

        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return false;

        int localActor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;
        if (!IsSightActiveForActor(localActor, turnIndex))
            return false;

        return TryGetSightSlotForActor(localActor, out slotIndex);
    }

    private bool IsSightRoomProperty(string key)
    {
        return !string.IsNullOrEmpty(key) && key.StartsWith(ROOM_PROP_SIGHT_PREFIX, StringComparison.Ordinal);
    }

    private bool ContainsSightRoomProperty(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged == null)
            return false;

        foreach (object keyObj in propertiesThatChanged.Keys)
        {
            if (keyObj is string key && IsSightRoomProperty(key))
                return true;
        }

        return false;
    }

    private void TryShowSightRevealForPlayer(Player targetPlayer)
    {
        if (targetPlayer == null || targetPlayer == PhotonNetwork.LocalPlayer)
            return;

        int localActor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;
        if (!TryGetSightStateForOwner(localActor, out OracleSightState state))
            return;

        if (state.targetActorNumber != targetPlayer.ActorNumber)
            return;

        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return;

        if (!IsSightRevealVisible(state, turnIndex))
        {
            RefreshSightViewsForPlanningRound();
            return;
        }

        bool ready = false;
        int submitTurn = -1;

        if (targetPlayer.CustomProperties.TryGetValue(PLAYER_PROP_READY, out object readyObj))
            ready = readyObj is bool readyValue && readyValue;

        if (targetPlayer.CustomProperties.TryGetValue(PLAYER_PROP_SUBMIT_TURN, out object turnObj))
            submitTurn = Convert.ToInt32(turnObj);

        if (!ready || submitTurn != turnIndex)
        {
            RefreshSightViewsForPlanningRound();
            return;
        }

        int[] enemyActions = GetPlayerActionsForTurn(targetPlayer, turnIndex);
        SlotSnapshot snapshot = BuildSnapshotFromSubmittedActions(enemyActions, state.watchedLogicalSlotIndex, targetPlayer.ActorNumber, turnIndex);
        snapshot.revision = GetNextAcceptedFallbackRevision(localActor);
        sightAcceptedSnapshotRevisions[localActor] = snapshot.revision;
        ApplySightSnapshotToEnemyPreview(state, snapshot);
    }

    private int GetNextAcceptedFallbackRevision(int sightOwnerActorNumber)
    {
        if (!sightAcceptedSnapshotRevisions.TryGetValue(sightOwnerActorNumber, out int revision))
            revision = 0;

        return revision + 1;
    }

    private SlotSnapshot BuildSnapshotFromSubmittedActions(int[] actions, int logicalSlotIndex, int ownerActorNumber, int planningRound)
    {
        SlotSnapshot snapshot = new SlotSnapshot
        {
            logicalSlotIndex = logicalSlotIndex,
            actionId = (int)ActionType.None,
            isEmpty = true,
            isLockedContinuation = false,
            sourceLogicalSlotIndex = -1,
            ownerActorNumber = ownerActorNumber,
            planningRound = planningRound,
            revision = 0
        };

        if (actions == null || logicalSlotIndex < 0 || logicalSlotIndex >= actions.Length)
            return snapshot;

        if (TryGetContinuationSourceAction(actions, logicalSlotIndex, out ActionType sourceAction))
        {
            snapshot.actionId = (int)sourceAction;
            snapshot.isEmpty = false;
            snapshot.isLockedContinuation = true;
            snapshot.sourceLogicalSlotIndex = GetContinuationSourceIndex(actions, logicalSlotIndex);
            return snapshot;
        }

        ActionType action = ToSafeActionType(actions[logicalSlotIndex], "SightRevealEnemy", logicalSlotIndex);
        snapshot.actionId = (int)action;
        snapshot.isEmpty = action == ActionType.None;
        return snapshot;
    }

    private int GetContinuationSourceIndex(int[] actions, int logicalSlotIndex)
    {
        if (actions == null || logicalSlotIndex <= 0)
            return -1;

        for (int i = 0; i < logicalSlotIndex; i++)
        {
            ActionType action = ToSafeActionType(actions[i], "SightRevealEnemy", i);
            int cost = GetSlotCost(action);
            if (cost > 1 && i + cost > logicalSlotIndex)
                return i;
        }

        return -1;
    }

    private void ShowSightRevealSlot(int[] enemyActions, int slotIndex)
    {
        int localActor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;
        if (!TryGetSightStateForOwner(localActor, out OracleSightState state))
            return;

        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return;

        SlotSnapshot snapshot = BuildSnapshotFromSubmittedActions(enemyActions, slotIndex, state.targetActorNumber, turnIndex);
        ApplySightSnapshotToEnemyPreview(state, snapshot);
    }

    private bool TryGetContinuationSourceAction(int[] actions, int slotIndex, out ActionType sourceAction)
    {
        sourceAction = ActionType.None;

        if (actions == null || slotIndex <= 0 || slotIndex >= actions.Length)
            return false;

        for (int i = 0; i < slotIndex; i++)
        {
            ActionType action = ToSafeActionType(actions[i], "SightRevealEnemy", i);
            int cost = GetSlotCost(action);

            if (cost > 1 && i + cost > slotIndex)
            {
                sourceAction = action;
                return true;
            }
        }

        return false;
    }

    private void SetSightContinuationPreview(Image target, ActionType sourceAction)
    {
        if (target == null)
            return;

        SlotSnapshot snapshot = new SlotSnapshot
        {
            logicalSlotIndex = 0,
            actionId = (int)sourceAction,
            isEmpty = false,
            isLockedContinuation = true,
            sourceLogicalSlotIndex = -1,
            ownerActorNumber = enemyActorNumber,
            planningRound = 0,
            revision = 0
        };

        target.sprite = GetSpriteForSnapshot(snapshot);
        target.color = new Color(1f, 1f, 1f, 0.55f);
    }

    public void ResetSightMatchState()
    {
        ClearSightEnergyLocksForMatch();
        sightAcceptedSnapshotRevisions.Clear();
        sightLocalSnapshotRevisions.Clear();

        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        Hashtable roomProps = PhotonNetwork.CurrentRoom.CustomProperties;

        if (roomProps != null)
        {
            foreach (DictionaryEntry entry in roomProps)
            {
                if (entry.Key is string key && IsSightRoomProperty(key))
                    props[key] = null;
            }
        }

        Player[] players = PhotonNetwork.PlayerList;
        if (players != null)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null)
                    continue;

                int actorNumber = players[i].ActorNumber;
                props[GetSightUsedKey(actorNumber)] = null;
                props[GetSightActiveKey(actorNumber)] = null;
                props[GetSightTargetKey(actorNumber)] = null;
                props[GetSightSlotKey(actorNumber)] = null;
                props[GetSightRoundKey(actorNumber)] = null;
            }
        }

        if (props.Count > 0)
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        Debug.Log("[OracleSight] Sight match-state reset.");
    }
}
