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
    private readonly HashSet<string> recordedUsageSteps = new HashSet<string>();

    private void RecordLocalActionUsage(int turn, int slot, ActionType action, bool chargedRelease)
    {
        if (chargedRelease || myUnit == null || !myUnit.IsMine() || !PhotonNetwork.InRoom) return;
        string characterId = LocalActionUsage.CharacterId(PhotonNetwork.LocalPlayer);
        if (LocalActionUsage.Category(characterId, action) < 0) return;
        // The resolving manager is scene-scoped. A replayed callback cannot count the same slot twice.
        if (recordedUsageSteps.Add(turn + ":" + slot)) LocalActionUsage.Record(characterId, action);
    }
    private IEnumerator ResolveActionsInOrderCoroutine(int[] myActions, int[] enemyActions)
    {
        if (gameEnded || isResolving)
            yield break;

        isResolving = true;
        SetPlanningInteractable(false);
        ApplyActionDisplayLayout();

        if (readyButton != null)
            readyButton.interactable = false;

        myActions = SanitizeConsecutiveJumpsForResolve(myActions, "My");
        enemyActions = SanitizeConsecutiveJumpsForResolve(enemyActions, "Enemy");

        myHeavyPendingThisTurn = false;
        enemyHeavyPendingThisTurn = false;
        myRiftPendingThisTurn = false;
        enemyRiftPendingThisTurn = false;
        PrepareEnemyActionsPreviewForResolution();

        int maxLen = Mathf.Max(myActions != null ? myActions.Length : 0, enemyActions != null ? enemyActions.Length : 0);
        int step = 1;

        for (int i = 0; i < maxLen; i++)
        {
            ActionType myAction = (myActions != null && i < myActions.Length) ? (ActionType)myActions[i] : ActionType.None;
            ActionType enemyAction = (enemyActions != null && i < enemyActions.Length) ? (ActionType)enemyActions[i] : ActionType.None;

            Debug.Log($"Resolving step {step}: mine={myAction}, enemy={enemyAction}");
            yield return StartCoroutine(ResolveSingleStepCoroutine(i, myAction, enemyAction, enemyActions));

            if (CheckGameResultAfterStep())
            {
                RestoreActionDisplayLayout();
                yield break;
            }

            if (pendingParryCounter)
                yield return StartCoroutine(ResolveParryCounterStepCoroutine());

            if (CheckGameResultAfterStep())
            {
                RestoreActionDisplayLayout();
                yield break;
            }

            step++;
        }

        RefreshDebug("Finished resolving turn actions.");

        ClearAllPlanningSlots();
        RestoreActionDisplayLayout();

        isResolving = false;
        resolveCoroutine = null;

        if (!gameEnded)
            RestartPlanningStartCoroutine();
    }

    private IEnumerator ResolveParryCounterStepCoroutine()
    {
        if (!pendingParryCounter)
            yield break;

        bool counterByMine = pendingParryCounterByMine;
        int damage = pendingParryCounterDamage;
        playedHitFeedbackThisStep = false;
        playedFinisherSlowMotionThisStep = false;

        pendingParryCounter = false;
        pendingParryCounterByMine = false;
        pendingParryCounterDamage = 0;

        CharacterUnit attacker = counterByMine ? myUnit : enemyUnit;
        CharacterUnit victim = counterByMine ? enemyUnit : myUnit;

        if (attacker == null || victim == null)
            yield break;

        Animator attackerAnimator = attacker.GetAnimator();

        // ParryCounter uses its own beat length.
        yield return StartCoroutine(PlayParryCounterAnimationAndWait(attackerAnimator));

        // Apply counter damage after the presentation cue.
        PlayHitFeedback(!counterByMine);

        if (playBloodOnParryCounter && bloodHitVFXManager != null)
            PlayBloodHitEffect(counterByMine,damage);

        if (HitStopManager.Instance != null)
            yield return StartCoroutine(HitStopManager.Instance.HitStopByBeat());

        // Refresh UI after counter damage resolves.
        if (counterByMine)
        {
            int hpBefore = enemyUnit != null ? enemyUnit.currentHP : enemyHP;

            if (enemyUnit != null)
                enemyUnit.TakeDamage(damage);

            enemyHP = enemyUnit != null ? enemyUnit.currentHP : Mathf.Max(0, enemyHP - damage);
            TryPlayFinisherSlowMotion(hpBefore, enemyHP);
        }
        else
        {
            int hpBefore = myUnit != null ? myUnit.currentHP : myHP;

            if (myUnit != null)
                myUnit.TakeDamage(damage);

            myHP = myUnit != null ? myUnit.currentHP : Mathf.Max(0, myHP - damage);
            TryPlayFinisherSlowMotion(hpBefore, myHP);
        }

        RefreshAllHPUI();

        // Add the same post-effect beat delay used by normal hit effects.
        float postCounterDelayBeats = 0f;

        if (playedFinisherSlowMotionThisStep)
            postCounterDelayBeats = Mathf.Max(effectDelayBeats, finisherSlowMotionBeats);
        else if (playedHitFeedbackThisStep)
            postCounterDelayBeats = effectDelayBeats;

        if (postCounterDelayBeats > 0f)
            yield return new WaitForSecondsRealtime(GetBeatSeconds(postCounterDelayBeats));
    }

    private void RefreshAllHPUI()
    {
        UpdateHPBars();
        UpdateEnergyBars();
    }

    private ActionType GetEffectiveActionForStep(ActionType selectedAction, bool isMine, out bool releaseHeavyNow)
    {
        releaseHeavyNow = false;

        if (isMine)
        {
            // Start a heavy charge and defer the release to a later turn.
            if (myHeavyPendingThisTurn)
            {
                myHeavyPendingThisTurn = false;
                releaseHeavyNow = true;
                return ActionType.HeavyAttack;
            }

            if (myRiftPendingThisTurn)
            {
                myRiftPendingThisTurn = false;
                releaseHeavyNow = true;
                return ActionType.Rift;
            }

            // A charged heavy releases when its configured turn is reached.
            if (selectedAction == ActionType.HeavyAttack)
            {
                myHeavyPendingThisTurn = true;
                return ActionType.HeavyAttack;
            }

            if (selectedAction == ActionType.Rift)
            {
                myRiftPendingThisTurn = true;
                return ActionType.Rift;
            }

            return selectedAction;
        }
        else
        {
            if (enemyHeavyPendingThisTurn)
            {
                enemyHeavyPendingThisTurn = false;
                releaseHeavyNow = true;
                return ActionType.HeavyAttack;
            }

            if (enemyRiftPendingThisTurn)
            {
                enemyRiftPendingThisTurn = false;
                releaseHeavyNow = true;
                return ActionType.Rift;
            }

            if (selectedAction == ActionType.HeavyAttack)
            {
                enemyHeavyPendingThisTurn = true;
                return ActionType.HeavyAttack;
            }

            if (selectedAction == ActionType.Rift)
            {
                enemyRiftPendingThisTurn = true;
                return ActionType.Rift;
            }

            return selectedAction;
        }
    }

    private IEnumerator ResolveSingleStepCoroutine(int logicalSlotIndex, ActionType myAction, ActionType enemyAction, int[] enemyActions)
    {
        RefreshAnimatorReferences();

        myJumping = false;
        enemyJumping = false;
        playedHitFeedbackThisStep = false;
        playedFinisherSlowMotionThisStep = false;
        myShiftInterruptedThisStep = false;
        enemyShiftInterruptedThisStep = false;
        myFadeInterruptedThisStep = false;
        enemyFadeInterruptedThisStep = false;

        if (battleStepPlayer == null)
        {
            Debug.LogError("ResolveSingleStepCoroutine: battleStepPlayer is missing.");
            yield break;
        }

        int currentTurn = 0;
        if (TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            currentTurn = turnIndex;

        bool myHeavyReleaseNow;
        bool enemyHeavyReleaseNow;

        ActionType myEffectiveAction = GetEffectiveActionForStep(myAction, true, out myHeavyReleaseNow);
        ActionType enemyEffectiveAction = GetEffectiveActionForStep(enemyAction, false, out enemyHeavyReleaseNow);

        // Decide effective actions and per-step state flags before presentation starts.
        myChargingHeavyThisStep = IsHeavyChargingThisStep(myEffectiveAction, myHeavyReleaseNow);
        enemyChargingHeavyThisStep = IsHeavyChargingThisStep(enemyEffectiveAction, enemyHeavyReleaseNow);
        myChargingRiftThisStep = IsRiftChargingThisStep(myEffectiveAction, myHeavyReleaseNow);
        enemyChargingRiftThisStep = IsRiftChargingThisStep(enemyEffectiveAction, enemyHeavyReleaseNow);
        myHeavyAnimationProtectedThisStep = IsHeavyAnimationProtectedThisStep(myEffectiveAction);
        enemyHeavyAnimationProtectedThisStep = IsHeavyAnimationProtectedThisStep(enemyEffectiveAction);
        myFadeInterruptedThisStep =
            myEffectiveAction == ActionType.Fade &&
            CanLightAttackInterruptOracleAction(enemyEffectiveAction, myEffectiveAction, true, myHeavyReleaseNow);
        enemyFadeInterruptedThisStep =
            enemyEffectiveAction == ActionType.Fade &&
            CanLightAttackInterruptOracleAction(myEffectiveAction, enemyEffectiveAction, false, enemyHeavyReleaseNow);
        myShiftInterruptedThisStep =
            myEffectiveAction == ActionType.Shift &&
            CanLightAttackInterruptOracleAction(enemyEffectiveAction, myEffectiveAction, true, myHeavyReleaseNow);
        enemyShiftInterruptedThisStep =
            enemyEffectiveAction == ActionType.Shift &&
            CanLightAttackInterruptOracleAction(myEffectiveAction, enemyEffectiveAction, false, enemyHeavyReleaseNow);

        ConfigureOracleVFXForStep(true, myEffectiveAction);
        ConfigureOracleVFXForStep(false, enemyEffectiveAction);

        RevealEnemyActionForResolveStep(logicalSlotIndex, enemyActions);

        // BattleStepPlayer receives BPM, beat duration, and tile distance from TurnPlanningManager.
        battleStepPlayer.SetBeatConfig(bpm, normalStepBeats);
        battleStepPlayer.SetWorldUnitsPerTile(WorldUnitsPerTile);
        battleStepPlayer.SetOracleMovementWallBounds(
            constrainOracleSpecialMovementToWall,
            oracleMovementWallMinX,
            oracleMovementWallMaxX);
        ActionData myEffectiveActionData = GetActionDataForPresentation(true, myEffectiveAction);
        ActionData enemyEffectiveActionData = GetActionDataForPresentation(false, enemyEffectiveAction);
        RecordLocalActionUsage(currentTurn, logicalSlotIndex, myEffectiveAction, myHeavyReleaseNow);

        // Normal steps occupy the configured normal beat count.
        yield return StartCoroutine(
            battleStepPlayer.PlayStep(
                myUnit,
                enemyUnit,
                myAnimator,
                enemyAnimator,
                myEffectiveAction,
                enemyEffectiveAction,
                currentTurn,
                myHeavyReleaseNow,
                enemyHeavyReleaseNow,
                normalStepBeats,
                myEffectiveActionData,
                enemyEffectiveActionData,
                () =>
                {
                    ApplyMovement(ref distance, myEffectiveAction, true);
                    ApplyMovement(ref distance, enemyEffectiveAction, false);
                },
                () =>
                {
                    if (myEffectiveAction == ActionType.Shift && enemyEffectiveAction == ActionType.Shift)
                    {
                        ApplySimultaneousOracleShift();
                        if (!myShiftInterruptedThisStep)
                            ShowLocalShiftReadyIndicator();
                        return;
                    }

                    if (myEffectiveAction == ActionType.Shift)
                    {
                        ApplyOracleShift(true);
                        if (!myShiftInterruptedThisStep)
                            ShowLocalShiftReadyIndicator();
                    }

                    if (enemyEffectiveAction == ActionType.Shift)
                        ApplyOracleShift(false);
                },
                myShiftInterruptedThisStep,
                enemyShiftInterruptedThisStep
            )
        );

        CompleteCurrentEnemyActionReveal();

        RefreshDistance();

        ResolveCombat(
            currentTurn,
            myEffectiveAction,
            enemyEffectiveAction,
            myHeavyReleaseNow,
            enemyHeavyReleaseNow
        );

        // Add beat-based effect delay after hit stop, blood, and fatal slow motion triggers.
        bool hasSlowMotionStep =
            myEffectiveAction == ActionType.Dance ||
            enemyEffectiveAction == ActionType.Dance;

        float postStepDelayBeats = 0f;

        if (playedFinisherSlowMotionThisStep)
            postStepDelayBeats = Mathf.Max(effectDelayBeats, finisherSlowMotionBeats);
        else if (playedHitFeedbackThisStep || hasSlowMotionStep)
            postStepDelayBeats = effectDelayBeats;

        if (postStepDelayBeats > 0f)
            yield return new WaitForSecondsRealtime(GetBeatSeconds(postStepDelayBeats));

        UpdateHPBars();
        UpdateEnergyBars();

        // Reset per-step presentation flags after the step completes.
        myChargingHeavyThisStep = false;
        enemyChargingHeavyThisStep = false;
        myChargingRiftThisStep = false;
        enemyChargingRiftThisStep = false;
        myShiftInterruptedThisStep = false;
        enemyShiftInterruptedThisStep = false;
        myFadeInterruptedThisStep = false;
        enemyFadeInterruptedThisStep = false;
        myHeavyAnimationProtectedThisStep = false;
        enemyHeavyAnimationProtectedThisStep = false;
    }    // =========================

    private ActionData GetActionDataForPresentation(bool isMine, ActionType action)
    {
        CharacterClassConfig config = isMine ? GetMyClassConfig() : GetEnemyClassConfig();
        return config != null ? config.GetActionData(action) : null;
    }

}
