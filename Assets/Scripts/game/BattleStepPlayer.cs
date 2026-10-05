using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleStepPlayer : MonoBehaviour
{
    [Header("Beat Settings")]
    [SerializeField] private float bpm = 120f;
    [SerializeField] private float defaultStepBeats = 2f;

    [Header("Movement Timing")]
    [SerializeField] private float moveDelaySeconds = 0.5f;   // Delay before movement presentation starts.
    [SerializeField] private float moveTravelSeconds = 0.5f;  // Duration of the movement presentation.

    [Header("Oracle VFX Timing")]
    [SerializeField, Range(0.05f, 0.95f)] private float oracleShiftRepositionMoment = 0.5f;

    [Header("Ultimate Startup Slow Motion")]
    [SerializeField] private bool playUltimateStartupSlowMotion = true;
    [SerializeField, Range(0.01f, 0.99f)] private float ultimateStartupSlowTimeScale = 0.2f;
    [SerializeField, Min(0f)] private float ultimateStartupSlowBeats = 0.5f;

    [Header("Attack Range Preview")]
    [SerializeField] private AttackRangePreviewManager attackRangePreviewManager;
    [SerializeField] private bool autoCreateAttackRangePreviewManager = true;
    [SerializeField, Min(0.01f)] private float attackRangeTelegraphLeadBeats = 1f;
    [SerializeField, Min(0.01f)] private float attackRangePostHitFadeSeconds = 0.12f;

    [Header("Movement Settings")]
    [SerializeField] private float moveStep = 1f;
    [SerializeField] private float minCharacterGap = 0.2f;
    [SerializeField] private bool verboseMovementCollisionLogs = false;
    [SerializeField] private bool constrainOracleSpecialMovementToWall = true;
    [SerializeField] private float oracleMovementWallMinX = -8.5f;
    [SerializeField] private float oracleMovementWallMaxX = 8.5f;

    [Header("Idle Return")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private float idleCrossFadeTime = 0f;

    private readonly HashSet<int> warnedMissingMovementColliderIds = new HashSet<int>();

    private void Awake()
    {
        if (autoCreateAttackRangePreviewManager)
            ResolveAttackRangePreviewManager();
    }

    public void SetBeatConfig(float newBpm, float newDefaultStepBeats)
    {
        bpm = newBpm;
        defaultStepBeats = newDefaultStepBeats;
    }

    public float WorldUnitsPerTile => Mathf.Max(0.01f, moveStep);

    public void SetWorldUnitsPerTile(float worldUnitsPerTile)
    {
        moveStep = Mathf.Max(0.01f, worldUnitsPerTile);
    }

    public void SetOracleMovementWallBounds(bool enabled, float minX, float maxX)
    {
        constrainOracleSpecialMovementToWall = enabled;
        oracleMovementWallMinX = minX;
        oracleMovementWallMaxX = maxX;
    }

    public float TilesToWorld(float tiles)
    {
        return tiles * WorldUnitsPerTile;
    }

    public float GetBeatDuration()
    {
        return 60f / bpm;
    }

    public static bool TryPlayForcedReaction(Animator animator, string stateName, float seconds, string fallback = null)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return false;
        if (!animator.HasState(0, Animator.StringToHash(stateName)))
        {
            if (string.IsNullOrEmpty(fallback) || !animator.HasState(0, Animator.StringToHash(fallback))) return false;
            stateName = fallback;
        }
        foreach (var parameter in animator.parameters)
            if (parameter.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(parameter.nameHash);
        animator.Play(stateName, 0, 0f);
        animator.Update(0f);
        var clips = animator.GetCurrentAnimatorClipInfo(0);
        animator.speed = clips.Length > 0 && clips[0].clip != null
            ? clips[0].clip.length / Mathf.Max(.01f, seconds) : 1f;
        return true;
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
        ActionData myActionData = null,
        ActionData enemyActionData = null,
        Action onMoveMoment = null,
        Action onShiftMoment = null,
        bool myShiftInterrupted = false,
        bool enemyShiftInterrupted = false)
    {
        float stepDuration = GetStepDuration(stepBeats);
        myUnit?.GetComponent<CombatImpactPresentation>()?.BeginAction(myAction, myHeavyReleaseNow, stepDuration);
        enemyUnit?.GetComponent<CombatImpactPresentation>()?.BeginAction(enemyAction, enemyHeavyReleaseNow, stepDuration);
        TryPlayUltimateStartupSlowMotion(myAction, enemyAction);

        float myOriginalSpeed = myAnimator != null ? myAnimator.speed : 1f;
        float enemyOriginalSpeed = enemyAnimator != null ? enemyAnimator.speed : 1f;

        StartCoroutine(ShowRangePreviewsForStep(
            myUnit,
            enemyUnit,
            myAction,
            enemyAction,
            myHeavyReleaseNow,
            enemyHeavyReleaseNow,
            myActionData,
            enemyActionData,
            stepDuration));

        PlayActionStartAudio(myUnit, myAction, myHeavyReleaseNow, myShiftInterrupted);
        PlayActionStartAudio(enemyUnit, enemyAction, enemyHeavyReleaseNow, enemyShiftInterrupted);

        if (myAnimator != null && myAction != ActionType.None && !IsDirectionalMoveAction(myAction))
            StartActionAnimation(myAnimator, myUnit, myAction, currentTurn, myHeavyReleaseNow, stepDuration);

        if (enemyAnimator != null && enemyAction != ActionType.None && !IsDirectionalMoveAction(enemyAction))
            StartActionAnimation(enemyAnimator, enemyUnit, enemyAction, currentTurn, enemyHeavyReleaseNow, stepDuration);

        StartOracleStepVFX(myUnit, enemyUnit, myAction, enemyAction, myHeavyReleaseNow, stepDuration, myShiftInterrupted);
        StartOracleStepVFX(enemyUnit, myUnit, enemyAction, myAction, enemyHeavyReleaseNow, stepDuration, enemyShiftInterrupted);

        float elapsed = 0f;

        bool myIsMove = IsMoveAction(myAction);
        bool enemyIsMove = IsMoveAction(enemyAction);
        bool anyMove = myIsMove || enemyIsMove;
        bool anyShift =
            (myAction == ActionType.Shift && !myShiftInterrupted) ||
            (enemyAction == ActionType.Shift && !enemyShiftInterrupted);

        if (anyMove || anyShift)
        {
            float moveStartTime = Mathf.Clamp(moveDelaySeconds, 0f, stepDuration);
            float moveTravelTime = Mathf.Clamp(moveTravelSeconds, 0.01f, stepDuration - moveStartTime);
            float shiftMoment = Mathf.Clamp(stepDuration * oracleShiftRepositionMoment, 0f, stepDuration);
            bool shiftApplied = false;

            if (anyShift && shiftMoment <= moveStartTime)
            {
                if (shiftMoment > 0f)
                {
                    yield return new WaitForSecondsRealtime(shiftMoment);
                    elapsed += shiftMoment;
                }

                onShiftMoment?.Invoke();
                shiftApplied = true;
            }

            // Wait until the movement cue, then choose animation from current positions.
            if (anyMove && moveStartTime > elapsed)
            {
                float wait = moveStartTime - elapsed;
                yield return new WaitForSecondsRealtime(wait);
                elapsed += wait;
            }

            if (anyMove)
            {
                PlayDirectionalMovementAudio(myUnit, myAction);
                PlayDirectionalMovementAudio(enemyUnit, enemyAction);

                StartDirectionalMovementAnimation(myAnimator, myUnit, enemyUnit, myAction, moveTravelTime);
                StartDirectionalMovementAnimation(enemyAnimator, enemyUnit, myUnit, enemyAction, moveTravelTime);

                // Notify TurnPlanningManager when gameplay distance should update.
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

            if (anyShift && !shiftApplied)
            {
                if (shiftMoment > elapsed)
                {
                    float wait = shiftMoment - elapsed;
                    yield return new WaitForSecondsRealtime(wait);
                    elapsed += wait;
                }

                onShiftMoment?.Invoke();
            }
        }

        float remain = stepDuration - elapsed;
        if (remain > 0f)
            yield return new WaitForSecondsRealtime(remain);

        // Movement actions return to Idle when the beat step ends.
        if (myIsMove && myAnimator != null)
            ForceToIdle(myAnimator);

        if (enemyIsMove && enemyAnimator != null)
            ForceToIdle(enemyAnimator);

        // Restore Animator.speed so the next step is unaffected.
        if (myAnimator != null)
            myAnimator.speed = myOriginalSpeed;

        if (enemyAnimator != null)
            enemyAnimator.speed = enemyOriginalSpeed;
    }

    private void PlayActionStartAudio(CharacterUnit unit, ActionType action, bool releaseNow, bool shiftInterrupted)
    {
        if (action == ActionType.None ||
            action == ActionType.Sight ||
            IsDirectionalMoveAction(action))
        {
            return;
        }

        if (action == ActionType.Shift && shiftInterrupted)
            return;

        AudioManager.Instance.PlayActionStart(unit, action, releaseNow);
    }

    private void PlayDirectionalMovementAudio(CharacterUnit unit, ActionType action)
    {
        if (!IsDirectionalMoveAction(action))
            return;

        AudioManager.Instance.PlayActionStart(unit, action, false);
    }

    private void StartOracleStepVFX(
        CharacterUnit caster,
        CharacterUnit target,
        ActionType action,
        ActionType targetAction,
        bool releaseNow,
        float stepDuration,
        bool shiftInterrupted)
    {
        OracleVFXController vfx = GetOracleVFX(caster);
        if (vfx == null)
            return;

        switch (action)
        {
            case ActionType.Bolt:
                if (targetAction == ActionType.Parry)
                    vfx.PlayBoltReflectedByParry(target, caster, stepDuration);
                else
                    vfx.PlayBoltCast(target, stepDuration);
                break;

            case ActionType.Rift:
                if (releaseNow)
                    vfx.PlayRiftBurst(target, stepDuration);
                else
                    vfx.PlayRiftWarning(target, stepDuration);
                break;

            case ActionType.Shift:
                if (shiftInterrupted)
                {
                    vfx.CancelShift();
                    break;
                }

                vfx.PlayShiftSwap(stepDuration);
                break;

            case ActionType.Ward:
                vfx.PlayWard(stepDuration);
                break;

            case ActionType.Fade:
                vfx.PlayFade(stepDuration);
                break;

            case ActionType.Sight:
                vfx.PlaySightActivation(stepDuration);
                break;
        }
    }

    private OracleVFXController GetOracleVFX(CharacterUnit unit)
    {
        if (unit == null)
            return null;

        return unit.GetComponentInChildren<OracleVFXController>(true);
    }

    private void TryPlayUltimateStartupSlowMotion(ActionType myAction, ActionType enemyAction)
    {
        if (!playUltimateStartupSlowMotion)
            return;

        if (myAction != ActionType.Ultimate && enemyAction != ActionType.Ultimate)
            return;

        if (HitStopManager.Instance == null)
            return;

        float duration = GetBeatDuration() * Mathf.Max(0f, ultimateStartupSlowBeats);
        if (duration <= 0f)
            return;

        HitStopManager.Instance.PlaySlowMotion(ultimateStartupSlowTimeScale, duration);
    }

    private IEnumerator ShowRangePreviewsForStep(
        CharacterUnit myUnit,
        CharacterUnit enemyUnit,
        ActionType myAction,
        ActionType enemyAction,
        bool myReleaseNow,
        bool enemyReleaseNow,
        ActionData myActionData,
        ActionData enemyActionData,
        float stepDuration)
    {
        if (!HasRangePreview(myAction, myActionData, myReleaseNow) && !HasRangePreview(enemyAction, enemyActionData, enemyReleaseNow))
            yield break;

        AttackRangePreviewManager manager = ResolveAttackRangePreviewManager();
        if (manager == null)
            yield break;

        float leadSeconds = Mathf.Max(0.01f, GetBeatDuration() * attackRangeTelegraphLeadBeats);
        float delay = Mathf.Max(0f, stepDuration - leadSeconds);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        manager.SetTileSize(WorldUnitsPerTile);
        manager.ClearTimedRanges();

        float visibleDuration = Mathf.Max(0.01f, Mathf.Min(leadSeconds, stepDuration - delay));
        ShowRangePreviewForAction(manager, myUnit, enemyUnit, myAction, myActionData, myReleaseNow, visibleDuration);
        ShowRangePreviewForAction(manager, enemyUnit, myUnit, enemyAction, enemyActionData, enemyReleaseNow, visibleDuration);
    }

    private void ShowRangePreviewForAction(
        AttackRangePreviewManager manager,
        CharacterUnit attacker,
        CharacterUnit defender,
        ActionType action,
        ActionData actionData,
        bool releaseNow,
        float visibleDuration)
    {
        if (manager == null || attacker == null || defender == null)
            return;

        if (!TryGetActionRangePreview(action, actionData, releaseNow, out int minRange, out int maxRange))
            return;

        manager.ShowRange(attacker, defender, minRange, maxRange, visibleDuration, attackRangePostHitFadeSeconds);
    }

    private bool HasRangePreview(ActionType action, ActionData actionData, bool releaseNow)
    {
        return TryGetActionRangePreview(action, actionData, releaseNow, out _, out _);
    }

    private bool TryGetActionRangePreview(ActionType action, ActionData actionData, bool releaseNow, out int minRange, out int maxRange)
    {
        minRange = 0;
        maxRange = 0;

        if ((action == ActionType.HeavyAttack || action == ActionType.Rift) && !releaseNow)
            return false;

        if (TryReadConfiguredRange(actionData, out minRange, out maxRange))
            return true;

        switch (action)
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

    private bool TryReadConfiguredRange(ActionData actionData, out int minRange, out int maxRange)
    {
        minRange = 0;
        maxRange = 0;

        if (actionData is AttackActionData attackData)
        {
            minRange = attackData.minRange > 0 ? attackData.minRange : 1;
            maxRange = attackData.range;
            return maxRange >= minRange;
        }

        if (actionData is UltimateActionData ultimateData)
        {
            minRange = ultimateData.minRange > 0 ? ultimateData.minRange : 1;
            maxRange = ultimateData.range;
            return maxRange >= minRange;
        }

        return false;
    }

    private AttackRangePreviewManager ResolveAttackRangePreviewManager()
    {
        if (attackRangePreviewManager != null)
            return attackRangePreviewManager;

        attackRangePreviewManager = GetComponent<AttackRangePreviewManager>();
        if (attackRangePreviewManager != null)
            return attackRangePreviewManager;

        attackRangePreviewManager = FindFirstObjectByType<AttackRangePreviewManager>();
        if (attackRangePreviewManager != null)
            return attackRangePreviewManager;

        if (!autoCreateAttackRangePreviewManager)
            return null;

        attackRangePreviewManager = gameObject.AddComponent<AttackRangePreviewManager>();
        return attackRangePreviewManager;
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

        string triggerName = GetTriggerName(action, currentTurn, heavyReleaseNow);
        string stateName = GetStateName(action, currentTurn, heavyReleaseNow);

        if (string.IsNullOrEmpty(triggerName))
            return;

        float clipLength = GetClipLength(animator, stateName);

        if (clipLength > 0.0001f)
            animator.speed = clipLength / stepDuration;
        else
            animator.speed = 1f;

        ResetActionTriggers(animator);
        animator.SetTrigger(triggerName);
        
        if (unit != null)
            unit.GetComponent<KnightSlashShaderVFX>()?.PlaySlash(action, heavyReleaseNow, stepDuration);
    }

    private void StartDirectionalMovementAnimation(
        Animator animator,
        CharacterUnit unit,
        CharacterUnit opponent,
        ActionType action,
        float animationDuration)
    {
        float movementDirection = GetMovementWorldDirection(action);
        if (animator == null || Mathf.Abs(movementDirection) < 0.001f)
            return;

        bool movingTowardEnemy = IsMovementTowardEnemy(unit, opponent, movementDirection);
        string triggerName = movingTowardEnemy ? "MoveForward" : "MoveBackward";
        float duration = Mathf.Max(0.01f, animationDuration);
        float clipLength = GetClipLength(animator, triggerName);

        animator.speed = clipLength > 0.0001f ? clipLength / duration : 1f;
        ResetActionTriggers(animator);
        animator.SetTrigger(triggerName);
        unit?.GetComponent<CharacterGroundPresentation>()?.BeginStride(duration);
    }

    private string GetTriggerName(ActionType action, int currentTurn, bool heavyReleaseNow)
    {
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
                return "MoveForward";

            case ActionType.MoveBackward:
                return "MoveBackward";

            case ActionType.Jump:
                return "Jump";

            case ActionType.Dance:
                return "Dance";

            case ActionType.Ultimate:
                return "Ultimate";

            case ActionType.Bolt:
                return "Bolt";

            case ActionType.Rift:
                return heavyReleaseNow ? "RiftRelease" : "RiftCharge";

            case ActionType.Shift:
                return "Shift";

            case ActionType.Ward:
                return "Ward";

            case ActionType.Fade:
                return "Fade";

            case ActionType.Sight:
                return "Sight";

            default:
                return null;
        }
    }

    private string GetStateName(ActionType action, int currentTurn, bool heavyReleaseNow)
    {
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
                return "MoveForward";

            case ActionType.MoveBackward:
                return "MoveBackward";

            case ActionType.Jump:
                return "Jump";

            case ActionType.Dance:
                return "Dance";

            case ActionType.Ultimate:
                return "Ultimate";

            case ActionType.Bolt:
                return "Bolt";

            case ActionType.Rift:
                return heavyReleaseNow ? "RiftRelease" : "RiftCharge";

            case ActionType.Shift:
                return "Shift";

            case ActionType.Ward:
                return "Ward";

            case ActionType.Fade:
                return "Fade";

            case ActionType.Sight:
                return "Sight";

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

        Debug.LogWarning("Animation clip not found: " + clipName);
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
        animator.ResetTrigger("Bolt");
        animator.ResetTrigger("RiftCharge");
        animator.ResetTrigger("RiftRelease");
        animator.ResetTrigger("Shift");
        animator.ResetTrigger("Ward");
        animator.ResetTrigger("Fade");
        animator.ResetTrigger("Sight");
    }

    private bool IsMoveAction(ActionType action)
    {
        return action == ActionType.MoveForward ||
               action == ActionType.MoveBackward;
    }

    private bool IsDirectionalMoveAction(ActionType action)
    {
        return action == ActionType.MoveForward ||
               action == ActionType.MoveBackward;
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

        Vector3 myTarget = myStart + GetMoveOffset(myUnit, enemyUnit, myAction, enemyAction);
        Vector3 enemyTarget = enemyStart + GetMoveOffset(enemyUnit, myUnit, enemyAction, myAction);

        if (ShouldResolveNoCrossTargets(myAction, enemyAction))
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

        RefreshFacingTowardOpponent(myUnit, enemyUnit);
        RefreshFacingTowardOpponent(enemyUnit, myUnit);
    }

    private void RefreshFacingTowardOpponent(CharacterUnit unit, CharacterUnit opponent)
    {
        if (unit == null || opponent == null)
            return;

        CharacterFacing facing = unit.GetComponent<CharacterFacing>();
        if (facing != null)
            facing.FaceTarget(opponent.transform);
    }

    private Vector3 GetMoveOffset(CharacterUnit selfUnit, CharacterUnit otherUnit, ActionType action, ActionType opposingAction)
    {
        float movementDirection = GetMovementWorldDirection(action);
        if (Mathf.Abs(movementDirection) > 0.001f)
            return Vector3.right * (movementDirection * TilesToWorld(1f));

        switch (action)
        {
            default:
                return Vector3.zero;
        }
    }

    private float GetMovementWorldDirection(ActionType action)
    {
        switch (action)
        {
            case ActionType.MoveForward:
                return 1f;

            case ActionType.MoveBackward:
                return -1f;

            default:
                return 0f;
        }
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

    private bool ShouldResolveNoCrossTargets(ActionType myAction, ActionType enemyAction)
    {
        return IsMoveAction(myAction) || IsMoveAction(enemyAction);
    }

    private Vector3 GetDirectionToOpponent(CharacterUnit selfUnit, CharacterUnit otherUnit)
    {
        if (selfUnit == null || otherUnit == null)
            return Vector3.right;

        return otherUnit.transform.position.x >= selfUnit.transform.position.x
            ? Vector3.right
            : Vector3.left;
    }

    private bool IsOracleSpecialMovementXInsideWall(float x)
    {
        if (!constrainOracleSpecialMovementToWall)
            return true;

        float minX = Mathf.Min(oracleMovementWallMinX, oracleMovementWallMaxX);
        float maxX = Mathf.Max(oracleMovementWallMinX, oracleMovementWallMaxX);
        const float tolerance = 0.001f;

        return x >= minX - tolerance && x <= maxX + tolerance;
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
        float leftFinalX = leftRawX;
        float rightFinalX = rightRawX;

        if (leftRawX > rightRawX - requiredDistance)
        {
            float overlap = (leftRawX + requiredDistance) - rightRawX;

            bool leftMoved = Mathf.Abs(leftRawX - leftStart.x) > 0.001f;
            bool rightMoved = Mathf.Abs(rightRawX - rightStart.x) > 0.001f;

            if (leftMoved && rightMoved)
            {
                leftFinalX -= overlap * 0.5f;
                rightFinalX += overlap * 0.5f;
            }
            else if (leftMoved)
            {
                leftFinalX -= overlap;
            }
            else if (rightMoved)
            {
                rightFinalX += overlap;
            }

            LogMovementCollisionCorrection(
                leftTransform,
                rightTransform,
                leftRawX,
                rightRawX,
                leftFinalX,
                rightFinalX,
                leftHalfWidth,
                rightHalfWidth,
                requiredDistance,
                leftMoved,
                rightMoved);
        }

        leftTarget.x = leftFinalX;
        rightTarget.x = rightFinalX;

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
            LogMissingMovementCollider(target, "SpriteRenderer bounds");

            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            return combined.extents.x;
        }

        LogMissingMovementCollider(target, "default half width");
        return 0.5f;
    }

    private void LogMovementCollisionCorrection(
        Transform leftTransform,
        Transform rightTransform,
        float leftRawX,
        float rightRawX,
        float leftFinalX,
        float rightFinalX,
        float leftHalfWidth,
        float rightHalfWidth,
        float requiredDistance,
        bool leftMoved,
        bool rightMoved)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!verboseMovementCollisionLogs)
            return;

        string leftName = leftTransform != null ? leftTransform.name : "Left";
        string rightName = rightTransform != null ? rightTransform.name : "Right";
        Debug.Log(
            "[MovementCollision] Corrected " +
            leftName + "/" + rightName +
            " raw=(" + leftRawX.ToString("0.###") + ", " + rightRawX.ToString("0.###") + ")" +
            " final=(" + leftFinalX.ToString("0.###") + ", " + rightFinalX.ToString("0.###") + ")" +
            " halfWidths=(" + leftHalfWidth.ToString("0.###") + ", " + rightHalfWidth.ToString("0.###") + ")" +
            " minCharacterGap=" + minCharacterGap.ToString("0.###") +
            " requiredDistance=" + requiredDistance.ToString("0.###") +
            " moved=(" + leftMoved + ", " + rightMoved + ")",
            this);
#endif
    }

    private void LogMissingMovementCollider(Transform target, string fallback)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (target == null)
            return;

        int id = target.GetInstanceID();
        if (!warnedMissingMovementColliderIds.Add(id))
            return;

        Debug.LogWarning(
            "[MovementCollision] " + target.name +
            " has no Collider2D for movement separation; using " + fallback + ".",
            target);
#endif
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
