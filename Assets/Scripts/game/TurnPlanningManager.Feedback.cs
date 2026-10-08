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
    private KnightUltimateVFX GetKnightUltimateVFX(bool isMine)
    {
        CharacterUnit unit = isMine ? myUnit : enemyUnit;
        if (unit == null)
            return null;

        return unit.GetComponentInChildren<KnightUltimateVFX>(true);
    }

    private void PlayKnightUltimateLightningEffect(bool attackerIsMine)
    {
        KnightUltimateVFX vfx = GetKnightUltimateVFX(attackerIsMine);
        if (vfx == null)
            return;

        vfx.PlayUltimateLightning();
    }

    private OracleVFXController GetOracleVFX(bool isMine)
    {
        CharacterUnit unit = isMine ? myUnit : enemyUnit;
        if (unit == null)
            return null;

        return unit.GetComponentInChildren<OracleVFXController>(true);
    }

    private void ConfigureOracleVFXForStep(bool isMine, ActionType action)
    {
        OracleVFXController vfx = GetOracleVFX(isMine);
        if (vfx == null)
            return;

        AttackActionData attackData = GetAttackData(isMine, action);
        int actionRange = attackData != null ? attackData.range : 0;
        int actionMinRange = attackData != null ? attackData.minRange : 0;
        int gridDistance = GetCurrentGridDistance();

        CharacterUnit actorUnit = isMine ? myUnit : enemyUnit;
        CharacterUnit targetUnit = isMine ? enemyUnit : myUnit;

        if (actorUnit == null || targetUnit == null)
        {
            vfx.SetCombatPresentationContext(WorldUnitsPerTile, actionRange, gridDistance, actionMinRange);
            return;
        }

        Vector3 actorGround = GetUnitGroundPosition(actorUnit);
        Vector3 targetGround = GetUnitGroundPosition(targetUnit);
        Vector3 effectGround = ResolveOracleEffectGroundPosition(actorGround, targetGround, action, actionMinRange, actionRange, gridDistance);

        vfx.SetCombatPresentationContext(WorldUnitsPerTile, actionRange, gridDistance, actionMinRange, actorGround, targetGround, effectGround);
    }

    private Vector3 ResolveOracleEffectGroundPosition(
        Vector3 actorGround,
        Vector3 targetGround,
        ActionType action,
        int actionMinRange,
        int actionRange,
        int gridDistance)
    {
        if (action == ActionType.Rift)
            return ResolveOracleRangeCenterGroundPosition(actorGround, targetGround, actionMinRange, actionRange);

        if (actionRange <= 0 || gridDistance <= actionRange)
            return targetGround;

        Vector3 direction = targetGround - actorGround;
        if (direction.sqrMagnitude < 0.0001f)
            return targetGround;

        return actorGround + direction.normalized * Mathf.Max(0.01f, TilesToWorld(actionRange));
    }

    private Vector3 ResolveOracleRangeCenterGroundPosition(Vector3 actorGround, Vector3 targetGround, int actionMinRange, int actionRange)
    {
        if (actionRange <= 0)
            return targetGround;

        float minRange = Mathf.Max(0, actionMinRange);
        float maxRange = Mathf.Max(minRange, actionRange);
        float centerRange = minRange > 0f && maxRange > minRange
            ? (minRange + maxRange) * 0.5f
            : maxRange;

        float direction = targetGround.x < actorGround.x ? -1f : 1f;
        Vector3 position = actorGround + Vector3.right * direction * TilesToWorld(centerRange);
        position.y = targetGround.y;
        position.z = targetGround.z;
        return position;
    }

    private Vector3 GetUnitGroundPosition(CharacterUnit unit)
    {
        if (unit == null)
            return Vector3.zero;

        Collider2D collider2D = unit.GetComponentInChildren<Collider2D>();
        if (collider2D != null)
            return new Vector3(collider2D.bounds.center.x, collider2D.bounds.min.y, unit.transform.position.z);

        Renderer renderer = unit.GetComponentInChildren<Renderer>();
        if (renderer != null)
            return new Vector3(renderer.bounds.center.x, renderer.bounds.min.y, unit.transform.position.z);

        return unit.transform.position;
    }

    private void PlayActionSuccessAudio(bool actorIsMine, ActionType action)
    {
        CharacterUnit unit = actorIsMine ? myUnit : enemyUnit;
        AudioManager.Instance.PlayActionStart(unit, action, true);
    }

    private void PlayAttackHitEnemyAudio(bool attackerIsMine)
    {
        CharacterUnit attacker = attackerIsMine ? myUnit : enemyUnit;
        AudioManager.Instance.PlayAttackHitEnemy(attacker);
    }

    private void PlayHitAudio(bool targetIsMine)
    {
        CharacterUnit target = targetIsMine ? myUnit : enemyUnit;
        AudioManager.Instance.PlayHit(target);
    }

    private void PlayEnergyChangedAudio(bool isMine, int before, int after)
    {
        CharacterUnit unit = isMine ? myUnit : enemyUnit;
        AudioManager.Instance.PlayEnergyChanged(unit, before, after, maxEnergy);
    }

    private void PlayKnightParrySuccessAudio(bool defenderIsMine)
    {
        if (!IsKnightClass(defenderIsMine))
            return;

        AudioManager.Instance.PlayKnightParrySuccess();
    }

    private void PlayOracleWardSuccessAudio(bool defenderIsMine)
    {
        if (!IsOracleClass(defenderIsMine))
            return;

        AudioManager.Instance.PlayOracleWardSuccess();
    }

    private void PlayOracleFadeSuccessAudio(bool defenderIsMine)
    {
        if (!IsOracleClass(defenderIsMine))
            return;

        AudioManager.Instance.PlayOracleFadeSuccess();
    }

    private void PlayKnightUltimateClashAudio()
    {
        AudioManager.Instance.PlayKnightUltimateClash();
    }

    private void PlayOracleBoltHit(bool attackerIsMine, bool targetIsMine)
    {
        OracleVFXController vfx = GetOracleVFX(attackerIsMine);
        if (vfx == null)
            vfx = GetOracleVFX(targetIsMine);

        CharacterUnit targetUnit = targetIsMine ? myUnit : enemyUnit;

        if (vfx == null || targetUnit == null)
            return;

        vfx.PlayBoltHit(targetUnit, GetBeatSeconds(effectDelayBeats));
    }

    private void PlayOracleWardSuccess(bool defenderIsMine)
    {
        PlayOracleWardSuccessAudio(defenderIsMine);

        OracleVFXController vfx = GetOracleVFX(defenderIsMine);
        if (vfx == null)
            return;

        CharacterUnit attacker = defenderIsMine ? enemyUnit : myUnit;
        if (attacker != null) vfx.PlayWardSuccess(GetBeatSeconds(effectDelayBeats), attacker.transform.position);
        else vfx.PlayWardSuccess(GetBeatSeconds(effectDelayBeats));
    }

    private void PlayOracleFadeSuccess(bool defenderIsMine)
    {
        PlayOracleFadeSuccessAudio(defenderIsMine);

        OracleVFXController vfx = GetOracleVFX(defenderIsMine);
        if (vfx == null)
            return;

        vfx.PlayFadeSuccess(GetBeatSeconds(effectDelayBeats));
    }

    private void StopOracleRiftWarning(bool riftUserIsMine)
    {
        OracleVFXController vfx = GetOracleVFX(riftUserIsMine);
        if (vfx == null)
            return;

        vfx.StopRiftWarning(true);
    }

    private void PlayBloodHitEffect(bool attackerIsMine,int damage)
    {
        if (bloodHitVFXManager == null)
            bloodHitVFXManager = FindFirstObjectByType<BloodHitVFXManager>();

        if (bloodHitVFXManager == null)
            return;

        CharacterUnit attacker = attackerIsMine ? myUnit : enemyUnit;
        CharacterUnit victim = attackerIsMine ? enemyUnit : myUnit;

        if (attacker == null || victim == null)
            return;

        bloodHitVFXManager.PlayBloodHit(attacker.transform, victim.transform,damage);
    }

    private void PlayParrySuccessEffect(bool defenderIsMine)
    {
        CharacterUnit targetUnit = defenderIsMine ? myUnit : enemyUnit;
        if (targetUnit == null)
            return;

        PlayKnightParrySuccessAudio(defenderIsMine);
        PlayKnightGetParry(!defenderIsMine);

        targetUnit.GetComponent<CombatImpactPresentation>()?.PlayResolvedImpact(defenderIsMine ? enemyUnit : myUnit, true);
        if (parrySuccessEffectPrefab == null)
            return;

        Vector3 spawnPos = targetUnit.transform.position + parrySuccessEffectOffset;
        GameObject fx = Instantiate(parrySuccessEffectPrefab, spawnPos, Quaternion.identity);

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.08f, 0.08f);

        // Destroy the prefab as a fallback when it does not manage its own lifetime.
        if (fx != null &&
            fx.GetComponent<ParrySuccessEffect>() == null &&
            fx.GetComponent<AutoDestroyEffect>() == null &&
            parryEffectLifeTime > 0f)
        {
            Destroy(fx, parryEffectLifeTime);
        }
    }

    private void TriggerBeatHitStop()
    {
        if (HitStopManager.Instance == null)
            return;

        HitStopManager.Instance.StartCoroutine(HitStopManager.Instance.HitStopByBeat());
    }

    private bool DidDamageBecomeFatal(int hpBefore, int hpAfter)
    {
        return hpBefore > 0 && hpAfter <= 0;
    }

    private void TryPlayFinisherSlowMotion(int hpBefore, int hpAfter)
    {
        if (!DidDamageBecomeFatal(hpBefore, hpAfter))
            return;

        PlayFinisherSlowMotionOnce();
    }

    private void PlayFinisherSlowMotionOnce()
    {
        if (playedFinisherSlowMotionThisStep)
            return;

        playedFinisherSlowMotionThisStep = true;

        if (HitStopManager.Instance != null)
            HitStopManager.Instance.PlaySlowMotion(finisherSlowTimeScale, GetBeatSeconds(finisherSlowMotionBeats));

        Debug.Log("Finisher slow motion triggered.");
    }

    private bool IsKnightClass(bool isMine)
    {
        CharacterClassConfig config = isMine ? GetMyClassConfig() : GetEnemyClassConfig();
        if (config == null) return false;

        return config.className == "Knight";
    }

    private void PlayKnightParryCounterFeedback(bool defenderIsMine)
    {
        CharacterUnit defenderUnit = defenderIsMine ? myUnit : enemyUnit;
        Animator defenderAnim = defenderIsMine ? myAnimator : enemyAnimator;

        if (defenderUnit == null)
            return;

        PlayKnightParrySuccessAudio(defenderIsMine);
        PlayKnightGetParry(!defenderIsMine);

        if (IsKnightClass(defenderIsMine) && defenderAnim != null)
            defenderAnim.SetTrigger("ParryCounter");

        defenderUnit.GetComponent<CombatImpactPresentation>()?.PlayResolvedImpact(defenderIsMine ? enemyUnit : myUnit, true);
        if (parrySuccessEffectPrefab != null)
        {
            Vector3 spawnPos = defenderUnit.transform.position + parrySuccessEffectOffset;
            GameObject fx = Instantiate(parrySuccessEffectPrefab, spawnPos, Quaternion.identity);

            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.08f, 0.08f);

            Destroy(fx, parryEffectLifeTime);
        }
    }

    private void PlayHitFeedback(bool targetIsMine)
    {
        CharacterUnit targetUnit = targetIsMine ? myUnit : enemyUnit;
        Animator targetAnim = targetIsMine ? myAnimator : enemyAnimator;

        PlayHitAudio(targetIsMine);
        targetUnit?.GetComponent<CombatImpactPresentation>()?.PlayResolvedImpact(targetIsMine ? enemyUnit : myUnit);

        bool targetIsHeavyAnimationProtected = targetIsMine
            ? myHeavyAnimationProtectedThisStep
            : enemyHeavyAnimationProtectedThisStep;

        // Heavy charge can suppress normal hit feedback while damage still applies.
        if (!targetIsHeavyAnimationProtected)
        {
            if (targetAnim != null)
                targetAnim.SetTrigger("Hit");
        }
        else
        {
            if (targetAnim != null)
                targetAnim.ResetTrigger("Hit");

            Debug.Log((targetIsMine ? "My" : "Enemy") + " heavy animation protected: damage applied without Hit animation.");
        }

        // Apply both screen shake and target shake for hit feedback.
        if (targetUnit != null)
            StartCoroutine(PlayHitShakeCoroutine(targetUnit.transform));

        playedHitFeedbackThisStep = true;
    }

    private IEnumerator PlayHitShakeCoroutine(Transform target)
    {
        if (target == null)
            yield break;

        Vector3 previousOffset = Vector3.zero;
        CharacterUnit shakenUnit = target.GetComponent<CharacterUnit>();
        float timer = 0f;

        while (timer < hitShakeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float decay = 1f - Mathf.Clamp01(timer / Mathf.Max(0.0001f, hitShakeDuration));
            float offsetX = UnityEngine.Random.Range(-hitShakeStrength, hitShakeStrength) * decay;
            if (target == null) yield break;
            Vector3 offset = new Vector3(offsetX, 0f, 0f);
            Vector3 delta = offset - previousOffset;
            target.position += delta;
            if (shakenUnit != null) shakenUnit.AddPresentationOffset(delta);
            previousOffset = offset;
            yield return null;
        }

        if (target != null) target.position -= previousOffset;
        if (shakenUnit != null) shakenUnit.AddPresentationOffset(-previousOffset);
    }

}
