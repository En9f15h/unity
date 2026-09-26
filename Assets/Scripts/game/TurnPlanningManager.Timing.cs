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
    private bool HasReachedServerTimestamp(int targetTimestamp)
    {
        int diff = PhotonNetwork.ServerTimestamp - targetTimestamp;
        return diff >= 0;
    }

    private void RestartPlanningStartCoroutine()
    {
        if (gameEnded)
            return;

        if (planningStartCoroutine != null)
        {
            StopCoroutine(planningStartCoroutine);
            planningStartCoroutine = null;
        }

        planningStartCoroutine = StartCoroutine(WaitForSyncedGameStart());
    }

    private int GetResolveStartTimestamp()
    {
        if (GameSceneStartSync.Instance != null)
            return GameSceneStartSync.Instance.GetNextBeatTimestamp(resolveLeadBeats);

        // Fall back to GameSceneStartSync shared beat timing; otherwise use local bpm.
        int beatMs = Mathf.RoundToInt((60f / bpm) * 1000f);
        return PhotonNetwork.ServerTimestamp + beatMs * Mathf.Max(1, resolveLeadBeats);
    }

    private IEnumerator WaitForResolveBeatThenStart(int turnIndex, int resolveStartTimestamp, int[] myActions, int[] enemyActions)
    {
        if (gameEnded)
            yield break;

        RefreshDebug($"Waiting for resolve beat. Turn {turnIndex}");

        while (!HasReachedServerTimestamp(resolveStartTimestamp))
        {
            if (gameEnded)
                yield break;

            yield return null;
        }

        if (gameEnded)
            yield break;

        RefreshDebug($"Resolve started. Turn {turnIndex}");

        resolveStartCoroutine = null;
        resolveCoroutine = StartCoroutine(ResolveActionsInOrderCoroutine(myActions, enemyActions));
    }

    private float GetBeatDuration()
    {
        return 60f / bpm;
    }

    private float GetBeatSeconds(float beats)
    {
        return GetBeatDuration() * beats;
    }

    private bool TryGetCurrentTurnInfo(out int turnIndex, out double turnStart, out double turnDur)
    {
        turnIndex = -1;
        turnStart = 0;
        turnDur = 0;

        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_TURN_INDEX, out object turnObj) ||
            turnObj == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_TURN_START, out object startObj) ||
            startObj == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_TURN_DUR, out object durObj) ||
            durObj == null)
            return false;

        turnIndex = System.Convert.ToInt32(turnObj);
        turnStart = System.Convert.ToDouble(startObj);
        turnDur = System.Convert.ToDouble(durObj);
        return true;
    }

    private double GetRemainingSeconds(double turnStart, double turnDur)
    {
        double elapsed = PhotonNetwork.Time - turnStart;
        return turnDur - elapsed;
    }

}
