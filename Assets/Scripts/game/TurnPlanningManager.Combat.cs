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
    private void ResolveCombat(int currentTurn, ActionType myAction, ActionType enemyAction, bool myHeavyReleaseNow, bool enemyHeavyReleaseNow)
    {
        if (myAction == ActionType.Dance)
            ApplyDance(true);

        if (enemyAction == ActionType.Dance)
            ApplyDance(false);

        if (myAction == ActionType.Sight)
            TryApplySight(true, currentTurn);

        if (enemyAction == ActionType.Sight)
            TryApplySight(false, currentTurn);

        if (myAction == ActionType.Fade)
            MarkFadeUsed(true);

        if (enemyAction == ActionType.Fade)
            MarkFadeUsed(false);

        // Resolve movement, heavy charge state, attacks, ultimate, and UI for one step.
        if (myAction == ActionType.Ultimate &&
            enemyAction == ActionType.Ultimate &&
            IsKnightClass(true) &&
            IsKnightClass(false))
        {
            ResolveKnightUltimateClash();
            return;
        }

        if (myAction == ActionType.Ultimate)
            TryApplyUltimate(true, enemyAction);

        if (enemyAction == ActionType.Ultimate)
            TryApplyUltimate(false, myAction);

        bool myAttackHandledByPriority = false;
        bool enemyAttackHandledByPriority = false;
        bool myHeavyInterrupted = false;
        bool enemyHeavyInterrupted = false;

        if (CanLightAttackInterruptOracleAction(myAction, enemyAction, false, enemyHeavyReleaseNow))
        {
            myAttackHandledByPriority = true;
            enemyAttackHandledByPriority = TryApplyLightAttackInterruptAgainstOracleAction(true, enemyAction);
        }
        else if (CanLightAttackInterruptOracleAction(enemyAction, myAction, true, myHeavyReleaseNow))
        {
            enemyAttackHandledByPriority = true;
            myAttackHandledByPriority = TryApplyLightAttackInterruptAgainstOracleAction(false, myAction);
        }
        else if (CanLightAttackInterruptHeavy(myAction, enemyAction))
        {
            myAttackHandledByPriority = true;
            enemyHeavyInterrupted = TryApplyLightAttackInterruptAgainstHeavy(true, enemyHeavyReleaseNow);
            enemyAttackHandledByPriority = enemyHeavyInterrupted;
        }
        else if (CanLightAttackInterruptHeavy(enemyAction, myAction))
        {
            enemyAttackHandledByPriority = true;
            myHeavyInterrupted = TryApplyLightAttackInterruptAgainstHeavy(false, myHeavyReleaseNow);
            myAttackHandledByPriority = myHeavyInterrupted;
        }

        if (IsAttack(myAction) && !myAttackHandledByPriority && !myHeavyInterrupted)
            TryApplyAttack(myAction, enemyAction, true, myHeavyReleaseNow);

        if (IsAttack(enemyAction) && !enemyAttackHandledByPriority && !enemyHeavyInterrupted)
            TryApplyAttack(enemyAction, myAction, false, enemyHeavyReleaseNow);
    }

    private void ResolveKnightUltimateClash()
    {
        UltimateActionData myUltimate = GetUltimateData(true);
        UltimateActionData enemyUltimate = GetUltimateData(false);

        if (myUltimate == null || enemyUltimate == null)
            return;


        int currentDistance = GetCurrentGridDistance();

        // Each ultimate clash participant is treated as taking one blocked hit for energy.
        if (currentDistance > 1)
        {
            Debug.Log("Knight ultimate clash granted block energy to both players.");
            ConsumeAllEnergy(true);
            ConsumeAllEnergy(false);
            return;
        }

        int myHalfDamage = Mathf.Max(1, myUltimate.damage / 2);
        int enemyHalfDamage = Mathf.Max(1, enemyUltimate.damage / 2);

        if (myUnit != null)
        {
            int hpBefore = myUnit.currentHP;
            myUnit.TakeDamage(enemyHalfDamage);
            myHP = myUnit.currentHP;
            TryPlayFinisherSlowMotion(hpBefore, myHP);
        }

        if (enemyUnit != null)
        {
            int hpBefore = enemyUnit.currentHP;
            enemyUnit.TakeDamage(myHalfDamage);
            enemyHP = enemyUnit.currentHP;
            TryPlayFinisherSlowMotion(hpBefore, enemyHP);
        }

        PlayHitFeedback(true);
        PlayHitFeedback(false);
        PlayKnightUltimateClashAudio();

        ConsumeAllEnergy(true);
        ConsumeAllEnergy(false);

        Debug.Log($"Knight ultimate clash resolved. Enemy half damage={enemyHalfDamage}, mine half damage={myHalfDamage}");
    }

    private bool IsAttack(ActionType action)
    {
        return action == ActionType.LightAttack ||
               action == ActionType.HeavyAttack ||
               action == ActionType.LowAttack ||
               action == ActionType.Bolt ||
               action == ActionType.Rift;
    }

    private AttackActionData GetAttackData(bool attackerIsMine, ActionType action)
    {
        CharacterClassConfig config = attackerIsMine ? GetMyClassConfig() : GetEnemyClassConfig();
        if (config == null) return null;

        return config.GetActionData(action) as AttackActionData;
    }

    private ActionType GetEffectiveActionForTurn(bool isMine, ActionType selectedAction, int currentTurn)
    {
        CharacterUnit unit = isMine ? myUnit : enemyUnit;

        if (unit == null)
            return selectedAction;

        if (unit.ShouldReleaseHeavy(currentTurn))
            return ActionType.HeavyAttack;

        return selectedAction;
    }

    private bool TryApplyLightAttackInterruptAgainstHeavy(bool lightAttackerIsMine, bool heavyReleaseNow)
    {
        AttackActionData lightAttackData = GetAttackData(lightAttackerIsMine, ActionType.LightAttack);
        if (lightAttackData == null)
            return false;

        bool heavyUserIsMine = !lightAttackerIsMine;
        bool defenderJumpingNow = lightAttackerIsMine ? enemyJumping : myJumping;
        int currentDistance = GetCurrentGridDistance();

        AttackResolutionResult result = CombatResolver.ResolveAttackDamage(
            lightAttackData,
            ActionType.HeavyAttack,
            defenderJumpingNow,
            currentDistance
        );

        if (result.outOfRange)
        {
            Debug.Log($"LightAttack priority check: out of range. Distance={currentDistance}, range={lightAttackData.range}. HeavyAttack continues.");
            return false;
        }

        if (result.parried)
        {
            int counterDamage = lightAttackData.damage;
            PlayParrySuccessEffect(heavyUserIsMine);
            TriggerBeatHitStop();
            QueueParryCounter(heavyUserIsMine, counterDamage);
            Debug.Log("LightAttack was parried during priority check. HeavyAttack continues and Parry grants no energy.");
            return false;
        }

        if (result.blocked)
        {
            AddEnergy(heavyUserIsMine, energyPerBlock);
            Debug.Log("LightAttack was defended during priority check. HeavyAttack continues.");
            return false;
        }

        if (result.evaded)
        {
            Debug.Log("LightAttack was evaded during priority check. HeavyAttack continues.");
            return false;
        }

        if (!result.hit || result.damage <= 0)
            return false;

        // Cancel heavy only after the light attack actually hits; occupied heavy slots are not refunded.
        ApplyAttackHit(lightAttackerIsMine, result.damage, true);
        CancelInterruptedHeavyAttack(heavyUserIsMine, heavyReleaseNow);
        return true;
    }

    private void ApplyAttackHit(bool attackerIsMine, int damage, bool forceHitFeedback)
    {
        bool defenderIsMine = !attackerIsMine;

        if (damage > 0)
            PlayAttackHitEnemyAudio(attackerIsMine);

        if (forceHitFeedback)
            PlayForcedHitFeedback(defenderIsMine);
        else
            PlayHitFeedback(defenderIsMine);

        bool fatalDamage = false;

        if (attackerIsMine)
        {
            int hpBefore = enemyUnit != null ? enemyUnit.currentHP : enemyHP;

            if (enemyUnit != null)
                enemyUnit.TakeDamage(damage);

            enemyHP = enemyUnit != null ? enemyUnit.currentHP : Mathf.Max(0, enemyHP - damage);
            fatalDamage = DidDamageBecomeFatal(hpBefore, enemyHP);
            PlayBloodHitEffect(true,damage);
            Debug.Log("Mine dealt damage=" + damage);
        }
        else
        {
            int hpBefore = myUnit != null ? myUnit.currentHP : myHP;

            if (myUnit != null)
                myUnit.TakeDamage(damage);

            myHP = myUnit != null ? myUnit.currentHP : Mathf.Max(0, myHP - damage);
            fatalDamage = DidDamageBecomeFatal(hpBefore, myHP);
            PlayBloodHitEffect(false,damage);
            Debug.Log("Enemy dealt damage=" + damage);
        }

        AddEnergy(attackerIsMine, energyPerHit);

        if (fatalDamage)
            PlayFinisherSlowMotionOnce();
    }

    private void PlayForcedHitFeedback(bool targetIsMine)
    {
        CharacterUnit targetUnit = targetIsMine ? myUnit : enemyUnit;
        Animator targetAnim = targetIsMine ? myAnimator : enemyAnimator;

        PlayHitAudio(targetIsMine);

        if (targetAnim != null)
        {
            // Use Hit as the cancel animation when a light attack interrupts HeavyAttack; replace here if a HeavyCancel trigger is added later.
            ResetActionTriggers(targetAnim);
            targetAnim.SetTrigger("Hit");
        }

        if (targetUnit != null)
            StartCoroutine(PlayHitShakeCoroutine(targetUnit.transform));

        playedHitFeedbackThisStep = true;
    }

    private void CancelInterruptedHeavyAttack(bool heavyUserIsMine, bool heavyReleaseNow)
    {
        if (heavyUserIsMine)
        {
            myHeavyPendingThisTurn = false;

            if (myUnit != null)
                myUnit.ClearHeavyCharge();
        }
        else
        {
            enemyHeavyPendingThisTurn = false;

            if (enemyUnit != null)
                enemyUnit.ClearHeavyCharge();
        }

        Debug.Log((heavyUserIsMine ? "My" : "Enemy") + " HeavyAttack interrupted by LightAttack. releaseStep=" + heavyReleaseNow);
    }

    private bool TryApplyLightAttackInterruptAgainstOracleAction(bool lightAttackerIsMine, ActionType oracleAction)
    {
        AttackActionData lightAttackData = GetAttackData(lightAttackerIsMine, ActionType.LightAttack);
        if (lightAttackData == null)
            return false;

        bool oracleUserIsMine = !lightAttackerIsMine;
        int currentDistance = GetCurrentGridDistance();

        AttackResolutionResult result = CombatResolver.ResolveAttackDamage(
            lightAttackData,
            ActionType.None,
            false,
            currentDistance
        );

        if (result.outOfRange || result.tooClose || !result.hit || result.damage <= 0)
            return false;

        ApplyAttackHit(lightAttackerIsMine, result.damage, true);

        if (oracleAction == ActionType.Rift)
            CancelInterruptedRift(oracleUserIsMine);

        if (oracleAction == ActionType.Fade)
            MarkFadeUsed(oracleUserIsMine);

        if (oracleAction == ActionType.Shift)
            MarkShiftUsed(oracleUserIsMine);

        Debug.Log((oracleUserIsMine ? "My" : "Enemy") + " " + oracleAction + " interrupted by close-range LightAttack.");
        return true;
    }

    private void CancelInterruptedRift(bool riftUserIsMine)
    {
        if (riftUserIsMine)
            myRiftPendingThisTurn = false;
        else
            enemyRiftPendingThisTurn = false;

        StopOracleRiftWarning(riftUserIsMine);
        Debug.Log((riftUserIsMine ? "My" : "Enemy") + " Rift was interrupted before release.");
    }

    private void MarkShiftUsed(bool shiftUserIsMine)
    {
        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return;

        if (shiftUserIsMine)
            myLastShiftTurn = turnIndex;
        else
            enemyLastShiftTurn = turnIndex;
    }

    private void MarkFadeUsed(bool fadeUserIsMine)
    {
        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return;

        if (fadeUserIsMine)
            myLastFadeTurn = turnIndex;
        else
            enemyLastFadeTurn = turnIndex;
    }

    private void TryApplyAttack(ActionType attackerAction, ActionType defenderAction, bool attackerIsMine, bool heavyReleaseNow)
    {
        CharacterUnit attackerUnit = attackerIsMine ? myUnit : enemyUnit;
        CharacterUnit defenderUnit = attackerIsMine ? enemyUnit : myUnit;

        if (attackerUnit == null || defenderUnit == null)
            return;

        AttackActionData attackData = GetAttackData(attackerIsMine, attackerAction);
        if (attackData == null)
            return;

        bool defenderJumpingNow = attackerIsMine ? enemyJumping : myJumping;
        bool defenderIsMine = !attackerIsMine;

        // Resolve normal attack interactions against parry, defense, jump, and range.
        if (attackerAction == ActionType.HeavyAttack && attackData.requiresCharge && !heavyReleaseNow)
        {
            Debug.Log((attackerIsMine ? "My" : "Enemy") + " heavy attack is charging; no damage this step.");
            return;
        }

        if (attackerAction == ActionType.Rift && !heavyReleaseNow)
        {
            Debug.Log((attackerIsMine ? "My" : "Enemy") + " Rift is charging; no damage this step.");
            return;
        }

        if (TryResolveOracleDefenseAgainstAttack(attackerAction, defenderAction, attackerIsMine, heavyReleaseNow))
            return;

        if (attackerAction == ActionType.Bolt || attackerAction == ActionType.Rift)
        {
            TryApplyOracleRangedAttack(attackerAction, defenderAction, attackerIsMine, attackData);
            return;
        }

        int currentDistance = GetCurrentGridDistance();

        AttackResolutionResult result = CombatResolver.ResolveAttackDamage(
            attackData,
            defenderAction,
            defenderJumpingNow,
            currentDistance
        );

        if (result.outOfRange)
        {
            Debug.Log($"Attack out of range. Distance={currentDistance}, range={attackData.range}");
            return;
        }

        if (result.tooClose)
        {
            Debug.Log($"Attack failed because the target is too close. Distance={currentDistance}, minRange={attackData.minRange}");
            return;
        }

        // Successful Parry queues a ParryCounter extra step.
        if (result.parried)
        {
            int counterDamage = attackData.damage;

            // Parry success inserts an extra counter step instead of applying normal damage now.
            PlayParrySuccessEffect(defenderIsMine);

            // Parry success no longer grants energy, but keeps the hit-stop rhythm effect.
            TriggerBeatHitStop();

            // Queue ParryCounter for the defender.
            QueueParryCounter(defenderIsMine, counterDamage);

            Debug.Log((attackerIsMine ? "Mine" : "Enemy") + " attack was parried; queued ParryCounter damage=" + counterDamage);
            return;
        }

        if (result.blocked)
        {
            Debug.Log((attackerIsMine ? "Mine" : "Enemy") + " attack was blocked by Defense.");
            AddEnergy(defenderIsMine, energyPerBlock);
            return;
        }

        if (result.evaded)
        {
            Debug.Log((attackerIsMine ? "Mine" : "Enemy") + " attack was evaded by Jump.");
            return;
        }

        if (result.hit && result.damage > 0)
        {
            ApplyAttackHit(attackerIsMine, result.damage, false);
        }
    }

    private bool TryResolveOracleDefenseAgainstAttack(ActionType attackerAction, ActionType defenderAction, bool attackerIsMine, bool heavyReleaseNow)
    {
        bool defenderIsMine = !attackerIsMine;

        if (!IsOracleClass(defenderIsMine))
            return false;

        AttackActionData attackData = GetAttackData(attackerIsMine, attackerAction);
        if (attackData == null)
            return false;

        int currentDistance = GetCurrentGridDistance();
        if (attackData.minRange > 0 && currentDistance < attackData.minRange)
            return false;

        if (currentDistance > attackData.range)
            return false;

        if (defenderAction == ActionType.Fade)
        {
            if (CanFadeEvadeAttack(attackerAction, attackerIsMine, heavyReleaseNow))
            {
                AddEnergy(defenderIsMine, energyPerBlock);
                PlayOracleFadeSuccess(defenderIsMine);
                Debug.Log((defenderIsMine ? "My" : "Enemy") + " Fade avoided " + attackerAction + ".");
                return true;
            }

            return false;
        }

        if (defenderAction == ActionType.Ward)
        {
            if (attackerAction == ActionType.LightAttack)
            {
                AddEnergy(defenderIsMine, energyPerBlock);
                PlayOracleWardSuccess(defenderIsMine);
                Debug.Log((defenderIsMine ? "My" : "Enemy") + " Ward blocked LightAttack.");
                return true;
            }

            if (attackerAction == ActionType.HeavyAttack)
            {
                ApplyAttackHit(attackerIsMine, 4, false);
                Debug.Log((defenderIsMine ? "My" : "Enemy") + " Ward reduced HeavyAttack to 4 damage.");
                return true;
            }

            if (attackerAction == ActionType.LowAttack)
            {
                ApplyAttackHit(attackerIsMine, 2, false);
                Debug.Log((defenderIsMine ? "My" : "Enemy") + " Ward was bypassed by LowAttack for 2 damage.");
                return true;
            }

            return false;
        }

        if (defenderAction == ActionType.Shift)
        {
            if (attackerAction == ActionType.HeavyAttack)
            {
                AddEnergy(defenderIsMine, energyPerBlock);
                Debug.Log((defenderIsMine ? "My" : "Enemy") + " Shift evaded HeavyAttack.");
                return true;
            }

            if (attackerAction == ActionType.LightAttack)
            {
                ApplyAttackHit(attackerIsMine, 3, false);
                Debug.Log((defenderIsMine ? "My" : "Enemy") + " Shift was beaten by LightAttack for 3 damage.");
                return true;
            }

            if (attackerAction == ActionType.LowAttack)
            {
                ApplyAttackHit(attackerIsMine, 2, false);
                Debug.Log((defenderIsMine ? "My" : "Enemy") + " Shift was hit by LowAttack for 2 damage.");
                return true;
            }

            return false;
        }

        return false;
    }

    private bool CanFadeEvadeAttack(ActionType attackerAction, bool attackerIsMine, bool releaseNow)
    {
        if (attackerAction == ActionType.HeavyAttack)
            return releaseNow && IsKnightClass(attackerIsMine);

        if (attackerAction == ActionType.Rift)
            return releaseNow;

        return false;
    }

    private void TryApplyOracleRangedAttack(
        ActionType attackerAction,
        ActionType defenderAction,
        bool attackerIsMine,
        AttackActionData attackData)
    {
        if (attackData == null)
            return;

        bool defenderIsMine = !attackerIsMine;
        bool defenderIsOracle = IsOracleClass(defenderIsMine);
        bool defenderJumpingNow = attackerIsMine ? enemyJumping : myJumping;
        int currentDistance = GetCurrentGridDistance();

        if (attackData.minRange > 0 && currentDistance < attackData.minRange)
        {
            Debug.Log($"{attackerAction} failed because the target is too close. Distance={currentDistance}, minRange={attackData.minRange}");
            return;
        }

        if (currentDistance > attackData.range)
        {
            Debug.Log($"{attackerAction} failed because the target is out of range. Distance={currentDistance}, range={attackData.range}");
            return;
        }

        if (attackerAction == ActionType.Bolt)
        {
            int boltDamage = GetBoltDamageByDistance(currentDistance, attackData);
            if (boltDamage <= 0)
            {
                Debug.Log($"Bolt failed because distance produced no damage. Distance={currentDistance}");
                return;
            }

            if (defenderIsOracle && defenderAction == ActionType.Ward)
            {
                AddEnergy(defenderIsMine, energyPerBlock);
                PlayOracleWardSuccess(defenderIsMine);
                Debug.Log((defenderIsMine ? "My" : "Enemy") + " Ward blocked Bolt.");
                return;
            }

            if (defenderAction == ActionType.Parry)
            {
                TriggerBeatHitStop();
                ApplyReflectedDamageToAttacker(attackerIsMine, boltDamage, false);
                Debug.Log("Bolt was reflected by Parry for damage=" + boltDamage + ".");
                return;
            }

            if (defenderAction == ActionType.Defense)
            {
                AddEnergy(defenderIsMine, energyPerBlock);
                Debug.Log("Bolt was blocked by Defense.");
                return;
            }

            PlayOracleBoltHit(attackerIsMine, defenderIsMine);
            ApplyAttackHit(attackerIsMine, boltDamage, false);
            Debug.Log("Bolt hit for damage=" + boltDamage + ". distance=" + currentDistance + ". defenderJumping=" + defenderJumpingNow);
            return;
        }

        if (attackerAction == ActionType.Rift)
        {
            if (defenderJumpingNow)
            {
                Debug.Log("Rift was avoided by Jump.");
                return;
            }

            int damage = attackData.damage;
            if (defenderIsOracle && defenderAction == ActionType.Ward)
            {
                Debug.Log((defenderIsMine ? "My" : "Enemy") + " Ward was pierced by Rift for full damage=" + damage + ".");
            }
            else if (defenderAction == ActionType.Defense)
            {
                damage = Mathf.Max(1, attackData.damage / 2);
                Debug.Log("Rift partially pierced Defense for damage=" + damage);
            }
            else if (defenderAction == ActionType.Parry)
            {
                Debug.Log("Rift ignored Parry.");
            }

            ApplyAttackHit(attackerIsMine, damage, false);
        }
    }

    private bool IsDistanceInsideAttackRange(AttackActionData attackData, int gridDistance)
    {
        if (attackData == null || gridDistance < 1)
            return false;

        if (attackData.minRange > 0 && gridDistance < attackData.minRange)
            return false;

        return gridDistance <= attackData.range;
    }

    private int GetBoltDamageByDistance(int gridDistance, AttackActionData boltData)
    {
        if (!IsDistanceInsideAttackRange(boltData, gridDistance))
            return 0;

        int maximumBoltDamage = boltData != null ? Mathf.Max(0, boltData.damage) : 3;
        return Mathf.Clamp(gridDistance - 1, 0, maximumBoltDamage);
    }

    private void ApplyReflectedDamageToAttacker(bool attackerIsMine, int damage, bool playBoltHit = true)
    {
        if (damage <= 0)
            return;

        CharacterUnit attackerUnit = attackerIsMine ? myUnit : enemyUnit;
        int hpBefore = attackerUnit != null ? attackerUnit.currentHP : (attackerIsMine ? myHP : enemyHP);

        if (playBoltHit)
            PlayOracleBoltHit(!attackerIsMine, attackerIsMine);

        PlayHitFeedback(attackerIsMine);

        if (attackerUnit != null)
            attackerUnit.TakeDamage(damage);

        if (attackerIsMine)
            myHP = attackerUnit != null ? attackerUnit.currentHP : Mathf.Max(0, myHP - damage);
        else
            enemyHP = attackerUnit != null ? attackerUnit.currentHP : Mathf.Max(0, enemyHP - damage);

        int hpAfter = attackerIsMine ? myHP : enemyHP;
        TryPlayFinisherSlowMotion(hpBefore, hpAfter);
    }

    private void QueueParryCounter(bool counterByMine, int damage)
    {
        pendingParryCounter = true;
        pendingParryCounterByMine = counterByMine;
        pendingParryCounterDamage = damage;
    }

    private void ApplyDance(bool isMine)
    {
        AddEnergy(isMine, 1);

      //  if (HitStopManager.Instance != null)
          //  HitStopManager.Instance.PlayDanceSlowMotionWithExtraTime(GetBeatSeconds(effectDelayBeats));

        Debug.Log((isMine ? "My" : "Enemy") + " dance: gain 1 energy and play slow motion.");
    }

    private void TryApplyUltimate(bool attackerIsMine, ActionType defenderAction)
    {
        UltimateActionData ultimateData = GetUltimateData(attackerIsMine);

        if (ultimateData == null)
        {
            Debug.LogWarning("TryApplyUltimate: ultimate data missing.");
            return;
        }

        int currentEnergy = attackerIsMine ? myEnergy : enemyEnergy;
        bool defenderIsMine = !attackerIsMine;

        if (currentEnergy < maxEnergy)
        {
            Debug.Log((attackerIsMine ? "My" : "Enemy") + " ultimate energy is not full.");
            return;
        }

        int currentDistance = GetCurrentGridDistance();
        if (IsKnightClass(attackerIsMine) && IsOracleClass(defenderIsMine))
        {
            ResolveKnightUltimateVsOracle(attackerIsMine, defenderAction, ultimateData, currentDistance);
            return;
        }

        // Ultimate defenders still gain block energy when defense applies.
        if (currentDistance > 1)
        {
            Debug.Log((attackerIsMine ? "Mine" : "Enemy") + " ultimate was blocked; defender gained 1 energy.");
            ConsumeAllEnergy(attackerIsMine);
            return;
        }

        // Ultimate can also be parried and queue a ParryCounter.
        if (defenderAction == ActionType.Parry)
        {
            int counterDamage = ultimateData.damage;

            // Parry success against ultimate inserts a counter step.
            PlayParrySuccessEffect(defenderIsMine);

            // Parry success no longer grants energy, but keeps the hit-stop rhythm effect.
            TriggerBeatHitStop();

            // Apply ultimate blood feedback before the counter step.
            ConsumeAllEnergy(attackerIsMine);

            // Queue the counter step for the defender.
            QueueParryCounter(defenderIsMine, counterDamage);

            Debug.Log((attackerIsMine ? "Mine" : "Enemy") + " ultimate was parried; queued ParryCounter damage=" + counterDamage);
            return;
        }

        // Defense can reduce ultimate damage.
        if (defenderAction == ActionType.Defense)
        {
            AddEnergy(defenderIsMine, energyPerBlock);
            ConsumeAllEnergy(attackerIsMine);

            Debug.Log((attackerIsMine ? "Mine" : "Enemy") + " ultimate was blocked by Defense.");
            return;
        }

        // Jump interactions for ultimate use the configured action data.

        int damage = ultimateData.damage;
        bool fatalDamage = false;

        if (damage > 0)
            PlayAttackHitEnemyAudio(attackerIsMine);

        if (attackerIsMine)
        {
            int hpBefore = enemyUnit != null ? enemyUnit.currentHP : enemyHP;

            if (enemyUnit != null)
                enemyUnit.TakeDamage(damage);

            enemyHP = enemyUnit != null ? enemyUnit.currentHP : Mathf.Max(0, enemyHP - damage);
            fatalDamage = DidDamageBecomeFatal(hpBefore, enemyHP);
            PlayHitFeedback(false);

            if (playBloodOnUltimate)
                PlayBloodHitEffect(true,damage);

            Debug.Log("Mine ultimate dealt damage " + damage + " after Jump interaction.");
        }
        else
        {
            int hpBefore = myUnit != null ? myUnit.currentHP : myHP;

            if (myUnit != null)
                myUnit.TakeDamage(damage);

            myHP = myUnit != null ? myUnit.currentHP : Mathf.Max(0, myHP - damage);
            fatalDamage = DidDamageBecomeFatal(hpBefore, myHP);
            PlayHitFeedback(true);

            if (playBloodOnUltimate)
                PlayBloodHitEffect(false,damage);

            Debug.Log("Enemy ultimate dealt damage " + damage + " after Jump interaction.");
        }

        ConsumeAllEnergy(attackerIsMine);

        if (fatalDamage)
            PlayFinisherSlowMotionOnce();
    }

    private void ResolveKnightUltimateVsOracle(
        bool attackerIsMine,
        ActionType defenderAction,
        UltimateActionData ultimateData,
        int currentDistance)
    {
        bool defenderIsMine = !attackerIsMine;

        if (ultimateData == null)
            return;

        if (ultimateData.minRange > 0 && currentDistance < ultimateData.minRange)
        {
            ConsumeAllEnergy(attackerIsMine);
            Debug.Log("Knight Ultimate missed Oracle because the target is too close. Distance=" + currentDistance + ", minRange=" + ultimateData.minRange);
            return;
        }

        if (currentDistance > ultimateData.range)
        {
            ConsumeAllEnergy(attackerIsMine);
            Debug.Log("Knight Ultimate missed Oracle after movement. Distance=" + currentDistance + ", range=" + ultimateData.range);
            return;
        }

        if (defenderAction == ActionType.Ward)
        {
            int wardReducedDamage = Mathf.Max(0, ultimateData.damage - 3);
            ApplyUltimateDamageToDefender(attackerIsMine, wardReducedDamage);
            ConsumeAllEnergy(attackerIsMine);
            Debug.Log("Knight Ultimate hit Oracle Ward for partial damage=" + wardReducedDamage + ".");
            return;
        }

        if (defenderAction == ActionType.Fade)
        {
            AddEnergy(defenderIsMine, energyPerBlock);
            PlayOracleFadeSuccess(defenderIsMine);
            ConsumeAllEnergy(attackerIsMine);
            Debug.Log("Oracle Fade avoided Knight Ultimate.");
            return;
        }

        if (defenderAction == ActionType.Jump)
        {
            int jumpReducedDamage = Mathf.Max(0, ultimateData.damage / 2);
            ApplyUltimateDamageToDefender(attackerIsMine, jumpReducedDamage);
            ConsumeAllEnergy(attackerIsMine);
            Debug.Log("Knight Ultimate hit Oracle Jump for half damage=" + jumpReducedDamage + ".");
            return;
        }

        int damage = ultimateData.damage;
        ApplyUltimateDamageToDefender(attackerIsMine, damage);
        ConsumeAllEnergy(attackerIsMine);

        if (defenderAction == ActionType.Shift)
            Debug.Log("Knight Ultimate hit Oracle Shift after movement for damage=" + damage + ". Distance=" + currentDistance);
        else if (defenderAction == ActionType.Sight)
            Debug.Log("Knight Ultimate hit Oracle Sight for damage=" + damage + ".");
        else
            Debug.Log("Knight Ultimate hit Oracle for damage=" + damage + ".");
    }

    private void ApplyUltimateDamageToDefender(bool attackerIsMine, int damage)
    {
        if (damage <= 0)
            return;

        PlayAttackHitEnemyAudio(attackerIsMine);

        bool fatalDamage;

        if (attackerIsMine)
        {
            int hpBefore = enemyUnit != null ? enemyUnit.currentHP : enemyHP;

            if (enemyUnit != null)
                enemyUnit.TakeDamage(damage);

            enemyHP = enemyUnit != null ? enemyUnit.currentHP : Mathf.Max(0, enemyHP - damage);
            fatalDamage = DidDamageBecomeFatal(hpBefore, enemyHP);
            PlayHitFeedback(false);

            if (playBloodOnUltimate)
                PlayBloodHitEffect(true,damage);
        }
        else
        {
            int hpBefore = myUnit != null ? myUnit.currentHP : myHP;

            if (myUnit != null)
                myUnit.TakeDamage(damage);

            myHP = myUnit != null ? myUnit.currentHP : Mathf.Max(0, myHP - damage);
            fatalDamage = DidDamageBecomeFatal(hpBefore, myHP);
            PlayHitFeedback(true);

            if (playBloodOnUltimate)
                PlayBloodHitEffect(false,damage);
        }

        if (fatalDamage)
            PlayFinisherSlowMotionOnce();
    }

    private void TryApplySight(bool userIsMine, int currentTurn)
    {
        UltimateActionData sightData = GetUltimateData(userIsMine);
        int currentEnergy = userIsMine ? myEnergy : enemyEnergy;
        int actorNumber = userIsMine ? myActorNumber : enemyActorNumber;

        if (sightData == null || actorNumber <= 0)
        {
            Debug.LogWarning("TryApplySight: Sight data or actor number is missing.");
            return;
        }

        if (IsSightUsedForActor(actorNumber))
        {
            Debug.Log((userIsMine ? "My" : "Enemy") + " Sight has already been used.");
            return;
        }

        if (currentEnergy < maxEnergy)
        {
            Debug.Log((userIsMine ? "My" : "Enemy") + " Sight energy is not full.");
            return;
        }

        LockSightEnergyFull(userIsMine);
        PlayActionSuccessAudio(userIsMine, ActionType.Sight);

        int localActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;
        if (actorNumber == localActorNumber)
        {
            int targetActorNumber = GetOpponentActorNumber(actorNumber);
            RequestSightActivation(actorNumber, targetActorNumber, currentTurn);
        }

        Debug.Log((userIsMine ? "My" : "Enemy") + " Sight resolved; activation confirmation will synchronize through Photon.");
    }

    private UltimateActionData GetUltimateData(bool isMine)
    {
        CharacterClassConfig config = isMine ? GetMyClassConfig() : GetEnemyClassConfig();
        if (config == null)
            return null;

        UltimateActionData ultimate = config.GetActionData(ActionType.Ultimate) as UltimateActionData;
        if (ultimate != null)
            return ultimate;

        return config.GetActionData(ActionType.Sight) as UltimateActionData;
    }

    private void AddEnergy(bool isMine, int value)
    {
        if (value <= 0)
            return;

        bool lockedFull = IsSightEnergyLockedFull(isMine);
        int before = GetEnergyValue(isMine);
        bool changed = SetEnergyValue(isMine, before + value);
        int after = GetEnergyValue(isMine);

        if (changed)
            PlayEnergyChangedAudio(isMine, before, after);

        Debug.Log((isMine ? "Mine" : "Enemy") + " energy is now " + after);

        if (!changed && lockedFull)
            return;

        // Preserve existing non-lock feedback behavior; locked full energy does not replay gain feedback when unchanged.
        TriggerBeatHitStop();
    }

    private void ConsumeAllEnergy(bool isMine)
    {
        SetEnergyValue(isMine, 0);
    }

    private int GetEnergyValue(bool isMine)
    {
        return isMine ? myEnergy : enemyEnergy;
    }

    private bool SetEnergyValue(bool isMine, int value, bool forceNotify = false)
    {
        int clampedValue = Mathf.Clamp(value, 0, maxEnergy);
        if (IsSightEnergyLockedFull(isMine))
            clampedValue = maxEnergy;

        int previousValue = GetEnergyValue(isMine);

        if (isMine)
            myEnergy = clampedValue;
        else
            enemyEnergy = clampedValue;

        bool changed = previousValue != clampedValue;
        if (changed || forceNotify)
        {
            NotifyEnergyChanged(isMine);
            UpdateEnergyBars();
        }

        return changed;
    }

    private bool IsSightEnergyLockedFull(bool isMine)
    {
        return isMine ? mySightEnergyLockedFull : enemySightEnergyLockedFull;
    }

    private void LockSightEnergyFull(bool isMine)
    {
        SetSightEnergyLockState(isMine, true);
    }

    private void SetSightEnergyLockState(bool isMine, bool locked)
    {
        bool wasLocked = IsSightEnergyLockedFull(isMine);

        if (isMine)
            mySightEnergyLockedFull = locked;
        else
            enemySightEnergyLockedFull = locked;

        if (locked)
            SetEnergyValue(isMine, maxEnergy, !wasLocked);
    }

    private void ApplySightEnergyLockForActor(int actorNumber)
    {
        if (actorNumber <= 0)
            return;

        if (actorNumber == myActorNumber)
            LockSightEnergyFull(true);
        else if (actorNumber == enemyActorNumber)
            LockSightEnergyFull(false);
    }

    private void SyncSightEnergyLocksFromSightState()
    {
        if (myActorNumber > 0 && IsSightUsedForActor(myActorNumber))
            LockSightEnergyFull(true);

        if (enemyActorNumber > 0 && IsSightUsedForActor(enemyActorNumber))
            LockSightEnergyFull(false);
    }

    private void ClearSightEnergyLocksForMatch()
    {
        mySightEnergyLockedFull = false;
        enemySightEnergyLockedFull = false;
    }

    private void EnforceSightEnergyLocks()
    {
        if (mySightEnergyLockedFull && myEnergy != maxEnergy)
            myEnergy = maxEnergy;

        if (enemySightEnergyLockedFull && enemyEnergy != maxEnergy)
            enemyEnergy = maxEnergy;
    }

    public int GetCurrentEnergyForUI(bool isMine)
    {
        EnforceSightEnergyLocks();
        return isMine ? myEnergy : enemyEnergy;
    }

    public int GetMaxEnergyForUI()
    {
        return maxEnergy;
    }

    public bool IsSightActiveOrUsedForUI(int actorNumber)
    {
        return actorNumber > 0 && (IsSightActivatedForActor(actorNumber) || IsSightUsedForActor(actorNumber));
    }

    private void NotifySightStateChanged(int actorNumber)
    {
        if (actorNumber <= 0)
            return;

        SightStateChanged?.Invoke(actorNumber, IsSightActiveOrUsedForUI(actorNumber));
    }

    private void NotifyKnownSightStatesChanged()
    {
        NotifySightStateChanged(myActorNumber);
        NotifySightStateChanged(enemyActorNumber);
    }

    private void NotifyEnergyChanged(bool isMine)
    {
        EnforceSightEnergyLocks();
        int currentEnergy = isMine ? myEnergy : enemyEnergy;
        EnergyChanged?.Invoke(isMine, currentEnergy, maxEnergy);
    }

    private void BindEnergyUI(EnergyBarUI energyUI, bool isMine, int actorNumber)
    {
        if (energyUI is OracleSightEnergyUI sightEnergyUI)
            sightEnergyUI.BindToEnergySource(this, isMine, actorNumber);
    }

    private void UpdateEnergyBars()
    {
        EnforceSightEnergyLocks();

        if (myEnergyBar != null)
        {
            myEnergyBar.SetEnergy(myEnergy, maxEnergy);

            if (myEnergyBar is OracleSightEnergyUI sightEnergyUI)
                sightEnergyUI.SetSightActive(IsSightActivatedForActor(myActorNumber) || IsSightUsedForActor(myActorNumber));
        }

        if (enemyEnergyBar != null)
        {
            enemyEnergyBar.SetEnergy(enemyEnergy, maxEnergy);

            if (enemyEnergyBar is OracleSightEnergyUI sightEnergyUI)
                sightEnergyUI.SetSightActive(IsSightActivatedForActor(enemyActorNumber) || IsSightUsedForActor(enemyActorNumber));
        }
    }

}
