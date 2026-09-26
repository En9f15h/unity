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
    private void UpdateHPBars()
    {
        if (myUnit != null)
            myHP = myUnit.currentHP;

        if (enemyUnit != null)
            enemyHP = enemyUnit.currentHP;

        if (myHPBar != null)
        {
            int max = myUnit != null ? myUnit.maxHP : 30;
            myHPBar.SetHP(myHP, max);
        }

        if (enemyHPBar != null)
        {
            int max = enemyUnit != null ? enemyUnit.maxHP : 30;
            enemyHPBar.SetHP(enemyHP, max);
        }
    }

    private bool CheckGameResultAfterStep()
    {
        UpdateHPBars();

        if (gameEnded)
            return true;

        bool myDead = myHP <= 0;
        bool enemyDead = enemyHP <= 0;

        if (!myDead && !enemyDead)
            return false;

        int winnerActorNumber = 0;

        if (myDead && !enemyDead)
            winnerActorNumber = enemyActorNumber;
        else if (!myDead && enemyDead)
            winnerActorNumber = myActorNumber;

        FinishGame(winnerActorNumber, "HPZero", true);
        return true;
    }

    private void ApplyGameResultFromRoom()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable roomProps = PhotonNetwork.CurrentRoom.CustomProperties;
        if (roomProps == null)
            return;

        if (!roomProps.TryGetValue(ROOM_PROP_GAME_ENDED, out object endedObj) || !Convert.ToBoolean(endedObj))
            return;

        int winnerActorNumber = 0;
        if (roomProps.TryGetValue(ROOM_PROP_GAME_WINNER_ACTOR, out object winnerObj))
            winnerActorNumber = Convert.ToInt32(winnerObj);

        string reason = "HPZero";
        if (roomProps.TryGetValue(ROOM_PROP_GAME_END_REASON, out object reasonObj) && reasonObj != null)
            reason = reasonObj.ToString();

        FinishGame(winnerActorNumber, reason, false);
    }

    private void FinishGame(int winnerActorNumber, string reason, bool publishToRoom)
    {
        if (gameEnded)
            return;

        gameEnded = true;
        localSubmitted = true;
        receivedResolution = true;
        isResolving = false;
        resolveCoroutine = null;
        pendingParryCounter = false;
        pendingParryCounterByMine = false;
        pendingParryCounterDamage = 0;

        if (planningStartCoroutine != null)
        {
            StopCoroutine(planningStartCoroutine);
            planningStartCoroutine = null;
        }

        if (resolveStartCoroutine != null)
        {
            StopCoroutine(resolveStartCoroutine);
            resolveStartCoroutine = null;
        }

        SetPlanningInteractable(false);
        ClearEnemyActionsPreview();
        RestoreActionDisplayLayout();

        if (readyButton != null)
            readyButton.interactable = false;

        if (countdownText != null)
            countdownText.text = "";

        RefreshPlanningPresentation(false);

        int localActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;
        GameResultManager.Instance.ShowResult(winnerActorNumber, localActorNumber, reason);

        RefreshDebug("Game ended. Winner actor = " + winnerActorNumber + ", reason = " + reason);

        if (publishToRoom)
            PublishGameResultToRoom(winnerActorNumber, reason);
    }

    private void PublishGameResultToRoom(int winnerActorNumber, string reason)
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable
        {
            { ROOM_PROP_GAME_ENDED, true },
            { ROOM_PROP_GAME_WINNER_ACTOR, winnerActorNumber },
            { ROOM_PROP_GAME_END_REASON, reason }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

}
