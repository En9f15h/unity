using System;
using System.Collections;
using UnityEngine;

public class BattleStepPlayer : MonoBehaviour
{
    [Header("節拍設定")]
    [SerializeField] private float bpm = 120f;
    [SerializeField] private float defaultStepBeats = 2f;

    [Header("移動時序")]
    [SerializeField] private float moveDelaySeconds = 0.5f;   // 先播動畫 0.5 秒
    [SerializeField] private float moveTravelSeconds = 0.5f;  // 再位移 0.5 秒

    [Header("位移設定")]
    [SerializeField] private float moveStep = 1f;
    [SerializeField] private float minCharacterGap = 0.2f;

    [Header("Idle 回切")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private float idleCrossFadeTime = 0f;

    public void SetBeatConfig(float newBpm, float newDefaultStepBeats)
    {
        bpm = newBpm;
        defaultStepBeats = newDefaultStepBeats;
    }

    public float GetBeatDuration()
    {
        return 60f / bpm;
    }

    public float GetStepDuration(float stepBeats = -1f)
    {
        float beats = stepBeats > 0f ? stepBeats : defaultStepBeats;
        return GetBeatDuration() * beats;
    }

    public IEnumerator PlayStep(
        CharacterUnit myUnit,
        CharacterUnit enemyUnit,
        Animator myAnimator,
        Animator enemyAnimator,
        ActionType myAction,
        ActionType enemyAction,
        int currentTurn,
        bool myHeavyReleaseNow,
        bool enemyHeavyReleaseNow,
        float stepBeats,
        Action onMoveMoment = null)
    {
        float stepDuration = GetStepDuration(stepBeats);

        float myOriginalSpeed = myAnimator != null ? myAnimator.speed : 1f;
        float enemyOriginalSpeed = enemyAnimator != null ? enemyAnimator.speed : 1f;

        if (myAnimator != null && myAction != ActionType.None)
            StartActionAnimation(myAnimator, myUnit, myAction, currentTurn, myHeavyReleaseNow, stepDuration);

        if (enemyAnimator != null && enemyAction != ActionType.None)
            StartActionAnimation(enemyAnimator, enemyUnit, enemyAction, currentTurn, enemyHeavyReleaseNow, stepDuration);
        float elapsed = 0f;

        bool myIsMove = IsMoveAction(myAction);
        bool enemyIsMove = IsMoveAction(enemyAction);
        bool anyMove = myIsMove || enemyIsMove;

        if (anyMove)
        {
            float moveStartTime = Mathf.Clamp(moveDelaySeconds, 0f, stepDuration);
            float moveTravelTime = Mathf.Clamp(moveTravelSeconds, 0.01f, stepDuration - moveStartTime);

            // 先播動畫，0.5 秒後才開始位移
            if (moveStartTime > 0f)
            {
                yield return new WaitForSecondsRealtime(moveStartTime);
                elapsed += moveStartTime;
            }

            // 真正開始位移的時刻，通知外部更新距離邏輯
            onMoveMoment?.Invoke();

            yield return StartCoroutine(
                PlayMovementStepTimed(
                    myUnit,
                    enemyUnit,
                    myAction,
                    enemyAction,
                    moveTravelTime
                )
            );

            elapsed += moveTravelTime;
        }

        float remain = stepDuration - elapsed;
        if (remain > 0f)
            yield return new WaitForSecondsRealtime(remain);

        // 這一步的拍點結束後，移動動作才切回 Idle
        if (myIsMove && myAnimator != null)
            ForceToIdle(myAnimator);

        if (enemyIsMove && enemyAnimator != null)
            ForceToIdle(enemyAnimator);

        // 還原 Animator.speed，避免影響下一步
        if (myAnimator != null)
            myAnimator.speed = myOriginalSpeed;

        if (enemyAnimator != null)
            enemyAnimator.speed = enemyOriginalSpeed;
    }

    private void StartActionAnimation(
        Animator animator,
        CharacterUnit unit,
        ActionType action,
        int currentTurn,
        bool heavyReleaseNow,
        float stepDuration)
    {
        if (animator == null || action == ActionType.None)
            return;

        string triggerName = GetTriggerName(unit, action, currentTurn, heavyReleaseNow);
        string stateName = GetStateName(unit, action, currentTurn, heavyReleaseNow);

        if (string.IsNullOrEmpty(triggerName))
            return;

        float clipLength = GetClipLength(animator, stateName);

        if (clipLength > 0.0001f)
            animator.speed = clipLength / stepDuration;
        else
            animator.speed = 1f;

        ResetActionTriggers(animator);
        animator.SetTrigger(triggerName);
    }

    private string GetTriggerName(CharacterUnit unit, ActionType action, int currentTurn, bool heavyReleaseNow)
    {
        bool mirrored = IsMirroredUnit(unit);

        switch (action)
        {
            case ActionType.LightAttack:
                return "LightAttack";

            case ActionType.HeavyAttack:
                return heavyReleaseNow ? "HeavyAttack" : "HeavyCharge";

            case ActionType.LowAttack:
                return "LowAttack";

            case ActionType.Parry:
                return "Parry";

            case ActionType.Defense:
                return "Defense";

            case ActionType.MoveForward:
                return mirrored ? "MoveBackward" : "MoveForward";

            case ActionType.MoveBackward:
                return mirrored ? "MoveForward" : "MoveBackward";

            case ActionType.Jump:
                return "Jump";

            case ActionType.Dance:
                return "Dance";

            case ActionType.Ultimate:
                return "Ultimate";

            default:
                return null;
        }
    }

    private string GetStateName(CharacterUnit unit, ActionType action, int currentTurn, bool heavyReleaseNow)
    {
        bool mirrored = IsMirroredUnit(unit);

        switch (action)
        {
            case ActionType.LightAttack:
                return "LightAttack";

            case ActionType.HeavyAttack:
                return heavyReleaseNow ? "HeavyAttack" : "HeavyCharge";

            case ActionType.LowAttack:
                return "LowAttack";

            case ActionType.Parry:
                return "Parry";

            case ActionType.Defense:
                return "Defense";

            case ActionType.MoveForward:
                return mirrored ? "MoveBackward" : "MoveForward";

            case ActionType.MoveBackward:
                return mirrored ? "MoveForward" : "MoveBackward";

            case ActionType.Jump:
                return "Jump";

            case ActionType.Dance:
                return "Dance";

            case ActionType.Ultimate:
                return "Ultimate";

            default:
                return null;
        }
    }

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

        Debug.LogWarning("找不到動畫 Clip: " + clipName);
        return 1f;
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
    }

    private bool IsMoveAction(ActionType action)
    {
        return action == ActionType.MoveForward || action == ActionType.MoveBackward;
    }

    private IEnumerator PlayMovementStepTimed(
        CharacterUnit myUnit,
        CharacterUnit enemyUnit,
        ActionType myAction,
        ActionType enemyAction,
        float duration)
    {
        Transform myTransform = myUnit != null ? myUnit.transform : null;
        Transform enemyTransform = enemyUnit != null ? enemyUnit.transform : null;

        if (myTransform == null && enemyTransform == null)
            yield break;

        Vector3 myStart = myTransform != null ? myTransform.position : Vector3.zero;
        Vector3 enemyStart = enemyTransform != null ? enemyTransform.position : Vector3.zero;

        // 這裡改成把角色本身傳進去，才能判斷鏡像後交換世界位移方向
        Vector3 myTarget = myStart + GetMoveOffset(myAction);
        Vector3 enemyTarget = enemyStart + GetMoveOffset(enemyAction);

        ResolveNoCrossTargets(ref myTarget, ref enemyTarget, myStart, enemyStart, myUnit, enemyUnit);

        float timer = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);

            if (myTransform != null)
                myTransform.position = Vector3.Lerp(myStart, myTarget, t);

            if (enemyTransform != null)
                enemyTransform.position = Vector3.Lerp(enemyStart, enemyTarget, t);

            yield return null;
        }

        if (myTransform != null)
            myTransform.position = myTarget;

        if (enemyTransform != null)
            enemyTransform.position = enemyTarget;
    }
    private Vector3 GetMoveOffset(ActionType action)
    {
        switch (action)
        {
            case ActionType.MoveForward:
                return Vector3.right * moveStep;

            case ActionType.MoveBackward:
                return Vector3.left * moveStep;

            default:
                return Vector3.zero;
        }
    }
    private bool IsMirroredUnit(CharacterUnit unit)
    {
        if (unit == null)
            return false;

        Animator animator = unit.GetAnimator();
        if (animator != null)
            return animator.transform.lossyScale.x < 0f;

        return unit.transform.lossyScale.x < 0f;
    }
    private void ResolveNoCrossTargets(
        ref Vector3 myTarget,
        ref Vector3 enemyTarget,
        Vector3 myStart,
        Vector3 enemyStart,
        CharacterUnit myUnit,
        CharacterUnit enemyUnit)
    {
        bool myIsLeft = myStart.x <= enemyStart.x;

        Vector3 leftStart = myIsLeft ? myStart : enemyStart;
        Vector3 rightStart = myIsLeft ? enemyStart : myStart;

        Vector3 leftTarget = myIsLeft ? myTarget : enemyTarget;
        Vector3 rightTarget = myIsLeft ? enemyTarget : myTarget;

        Transform leftTransform = myIsLeft ? (myUnit != null ? myUnit.transform : null)
                                           : (enemyUnit != null ? enemyUnit.transform : null);

        Transform rightTransform = myIsLeft ? (enemyUnit != null ? enemyUnit.transform : null)
                                            : (myUnit != null ? myUnit.transform : null);

        float leftHalfWidth = GetCharacterHalfWidth(leftTransform);
        float rightHalfWidth = GetCharacterHalfWidth(rightTransform);

        float requiredDistance = leftHalfWidth + rightHalfWidth + minCharacterGap;

        float leftRawX = leftTarget.x;
        float rightRawX = rightTarget.x;

        if (leftRawX > rightRawX - requiredDistance)
        {
            float overlap = (leftRawX + requiredDistance) - rightRawX;

            bool leftMoved = Mathf.Abs(leftRawX - leftStart.x) > 0.001f;
            bool rightMoved = Mathf.Abs(rightRawX - rightStart.x) > 0.001f;

            if (leftMoved && rightMoved)
            {
                leftRawX -= overlap * 0.5f;
                rightRawX += overlap * 0.5f;
            }
            else if (leftMoved)
            {
                leftRawX -= overlap;
            }
            else if (rightMoved)
            {
                rightRawX += overlap;
            }
        }

        leftTarget.x = leftRawX;
        rightTarget.x = rightRawX;

        if (myIsLeft)
        {
            myTarget = leftTarget;
            enemyTarget = rightTarget;
        }
        else
        {
            myTarget = rightTarget;
            enemyTarget = leftTarget;
        }
    }

    private float GetCharacterHalfWidth(Transform target)
    {
        if (target == null)
            return 0.5f;

        Collider2D col2D = target.GetComponentInChildren<Collider2D>();
        if (col2D != null)
            return col2D.bounds.extents.x;

        SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            return combined.extents.x;
        }

        return 0.5f;
    }

    private void ForceToIdle(Animator animator)
    {
        if (animator == null || string.IsNullOrEmpty(idleStateName))
            return;

        if (idleCrossFadeTime <= 0f)
            animator.Play(idleStateName, 0, 0f);
        else
            animator.CrossFadeInFixedTime(idleStateName, idleCrossFadeTime);
    }
}