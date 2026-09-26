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
    private float GetClipLength(Animator animator, string clipName)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(clipName))
            return 1f;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name == clipName)
                return clips[i].length;
        }

        Debug.LogWarning("Animation clip not found: " + clipName);
        return 1f;
    }

    private IEnumerator PlayParryCounterAnimationAndWait(Animator animator)
    {
        if (animator == null)
            yield break;

        float originalSpeed = animator.speed;

        string stateName = "ParryCounter";
        string triggerName = "ParryCounter";

        float stepDuration = GetBeatSeconds(parryCounterStepBeats);
        float clipLength = GetClipLength(animator, stateName);

        if (clipLength > 0.0001f)
            animator.speed = clipLength / stepDuration;
        else
            animator.speed = 1f;

        ResetActionTriggers(animator);
        animator.SetTrigger(triggerName);

        float enterTimeout = 0.25f;
        while (enterTimeout > 0f)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);

            if (current.IsName(stateName) || next.IsName(stateName))
                break;

            enterTimeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(stepDuration);

        animator.speed = originalSpeed;
    }

    private void PlayActionAnimation(Animator animator, ActionType action, bool isMine, int currentTurn)
    {
        if (animator == null)
            return;

        ResetActionTriggers(animator);

        CharacterUnit unit = isMine ? myUnit : enemyUnit;
        AttackActionData attackData = GetAttackData(isMine, action);

        switch (action)
        {
            case ActionType.LightAttack:
                animator.SetTrigger("LightAttack");
                break;

            case ActionType.HeavyAttack:
                if (attackData != null && attackData.requiresCharge)
                {
                    if (unit != null && unit.ShouldReleaseHeavy(currentTurn))
                        animator.SetTrigger("HeavyAttack");
                    else
                        animator.SetTrigger("HeavyCharge");
                }
                else
                {
                    animator.SetTrigger("HeavyAttack");
                }
                break;

            case ActionType.LowAttack:
                animator.SetTrigger("LowAttack");
                break;

            case ActionType.Parry:
                animator.SetTrigger("Parry");
                break;

            case ActionType.Defense:
                animator.SetTrigger("Defense");
                break;

            case ActionType.MoveForward:
            case ActionType.MoveBackward:
                SetDirectionalMovementTrigger(animator, action, isMine);
                break;

            case ActionType.Jump:
                animator.SetTrigger("Jump");
                break;

            case ActionType.Dance:
                animator.SetTrigger("Dance");
                break;

            case ActionType.Ultimate:
                animator.SetTrigger("Ultimate");
                break;

            case ActionType.Bolt:
                animator.SetTrigger("Bolt");
                break;

            case ActionType.Rift:
                animator.SetTrigger("RiftCharge");
                break;

            case ActionType.Shift:
                animator.SetTrigger("Shift");
                break;

            case ActionType.Ward:
                animator.SetTrigger("Ward");
                break;

            case ActionType.Fade:
                animator.SetTrigger("Fade");
                break;

            case ActionType.Sight:
                animator.SetTrigger("Sight");
                break;
        }
    }

    private void ResetActionTriggers(Animator animator)
    {
        if (animator == null)
            return;

        animator.ResetTrigger("MoveForward");
        animator.ResetTrigger("MoveBackward");
        animator.ResetTrigger("Jump");
        animator.ResetTrigger("LightAttack");
        animator.ResetTrigger("HeavyCharge");
        animator.ResetTrigger("HeavyAttack");
        animator.ResetTrigger("LowAttack");
        animator.ResetTrigger("Parry");
        animator.ResetTrigger("ParryCounter");
        animator.ResetTrigger("Defense");
        animator.ResetTrigger("Hit");
        animator.ResetTrigger("Dance");
        animator.ResetTrigger("Ultimate");
        animator.ResetTrigger("Bolt");
        animator.ResetTrigger("RiftCharge");
        animator.ResetTrigger("RiftRelease");
        animator.ResetTrigger("Shift");
        animator.ResetTrigger("Ward");
        animator.ResetTrigger("Fade");
        animator.ResetTrigger("Sight");
    }

    private void RefreshAnimatorReferences()
    {
        if (myUnit != null && myAnimator == null)
            myAnimator = myUnit.GetAnimator();

        if (enemyUnit != null && enemyAnimator == null)
            enemyAnimator = enemyUnit.GetAnimator();
    }

    private string GetAnimationStateName(ActionType action, bool isMine)
    {
        if (action == ActionType.None)
            return null;

        ActionAnimationMap[] maps = isMine ? myAnimationMaps : enemyAnimationMaps;
        if (maps == null) return null;

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null && maps[i].actionType == action)
                return maps[i].stateName;
        }

        return null;
    }

    private void PlayActionAnimation(Animator animator, ActionType action, bool isMine, int currentTurn, bool heavyReleaseNow)
    {
        if (animator == null)
            return;

        ResetActionTriggers(animator);

        switch (action)
        {
            case ActionType.LightAttack:
                animator.SetTrigger("LightAttack");
                break;

            case ActionType.HeavyAttack:
                if (heavyReleaseNow)
                    animator.SetTrigger("HeavyAttack");
                else
                    animator.SetTrigger("HeavyCharge");
                break;

            case ActionType.LowAttack:
                animator.SetTrigger("LowAttack");
                break;

            case ActionType.Parry:
                animator.SetTrigger("Parry");
                break;

            case ActionType.Defense:
                animator.SetTrigger("Defense");
                break;

            case ActionType.MoveForward:
            case ActionType.MoveBackward:
                SetDirectionalMovementTrigger(animator, action, isMine);
                break;

            case ActionType.Jump:
                animator.SetTrigger("Jump");
                break;

            case ActionType.Dance:
                animator.SetTrigger("Dance");
                break;

            case ActionType.Ultimate:
                animator.SetTrigger("Ultimate");
                break;

            case ActionType.Bolt:
                animator.SetTrigger("Bolt");
                break;

            case ActionType.Rift:
                animator.SetTrigger(heavyReleaseNow ? "RiftRelease" : "RiftCharge");
                break;

            case ActionType.Shift:
                animator.SetTrigger("Shift");
                break;

            case ActionType.Ward:
                animator.SetTrigger("Ward");
                break;

            case ActionType.Fade:
                animator.SetTrigger("Fade");
                break;

            case ActionType.Sight:
                animator.SetTrigger("Sight");
                break;
        }
    }

    private void SetDirectionalMovementTrigger(Animator animator, ActionType action, bool isMine)
    {
        if (animator == null)
            return;

        float movementDirection = GetMovementWorldDirection(action);
        if (Mathf.Abs(movementDirection) < 0.001f)
            return;

        CharacterUnit unit = isMine ? myUnit : enemyUnit;
        CharacterUnit opponent = isMine ? enemyUnit : myUnit;
        bool movingTowardEnemy = IsMovementTowardEnemy(unit, opponent, movementDirection);
        animator.SetTrigger(movingTowardEnemy ? "MoveForward" : "MoveBackward");
    }

    private bool IsMovementTowardEnemy(CharacterUnit unit, CharacterUnit opponent, float movementDirection)
    {
        float normalizedMovement = Mathf.Sign(movementDirection);
        if (Mathf.Abs(normalizedMovement) < 0.001f)
            return false;

        if (unit == null || opponent == null)
            return normalizedMovement > 0f;

        float enemyDelta = opponent.transform.position.x - unit.transform.position.x;
        if (Mathf.Abs(enemyDelta) < 0.001f)
            return normalizedMovement > 0f;

        return Mathf.Sign(enemyDelta) == normalizedMovement;
    }

}
