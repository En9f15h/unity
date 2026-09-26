using System.Collections;
using UnityEngine;

public class OracleVFXController : MonoBehaviour
{
    private const string BoltProjectilePrefabResourcePath = "Prefab/Oracle/vfx/Oracle_BoltVFX";
    private const string PrefabRootAnchorName = "Root";
    private const string PrefabVisualRootAnchorName = "VisualRoot";
    private const string EffectSortingLayerName = "Effect";
    private const string LegacyEffectSortingLayerName = "effect";
    private const int ParticleSortingOrder = -1;

    [Header("Anchors")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform castPoint;
    [SerializeField] private Transform groundVFXPoint;
    [SerializeField] private Transform centerVFXPoint;
    [SerializeField] private Transform vfxRoot;

    [Header("Prefabs")]
    [SerializeField] private GameObject boltProjectilePrefab;
    [SerializeField] private GameObject boltHitPrefab;
    [SerializeField] private GameObject riftWarningPrefab;
    [SerializeField] private GameObject riftBurstPrefab;
    [SerializeField] private GameObject shiftVFXPrefab;
    [SerializeField] private GameObject shiftExitFXPrefab;
    [SerializeField] private GameObject shiftEnterFXPrefab;
    [SerializeField] private GameObject wardVFXPrefab;
    [SerializeField] private GameObject wardSuccessPrefab;
    [SerializeField] private GameObject fadeDissolvePrefab;
    [SerializeField] private GameObject sightActivationPrefab;

    [Header("Shared Runtime FX")]
    [SerializeField] private Material additiveSpriteMaterial;
    [SerializeField] private bool useTemporaryLight2DPulses = true;
    [SerializeField] private bool verboseVfxLogs;
    [SerializeField] private bool showTileDebug;
    [SerializeField] private Color oracleMagicColor = new Color(0.35f, 0.82f, 1f, 0.82f);
    [SerializeField] private Color oracleDeepMagicColor = new Color(0.62f, 0.35f, 1f, 0.78f);
    [SerializeField] private Color oracleGoldMagicColor = new Color(1f, 0.82f, 0.32f, 0.9f);
    [SerializeField] private float minimumEffectDuration = 0.05f;
    [SerializeField] private int groundSortingOffset = -1;
    [SerializeField] private int frontSortingOffset = 3;
    [SerializeField] private int highSortingOffset = 8;

    [Header("Bolt FX")]
    [SerializeField] private float boltCastFlashDuration = 0.16f;
    [SerializeField, Range(0.05f, 0.8f)] private float boltLaunchDuration = 0.22f;
    [SerializeField, Range(0f, 0.3f)] private float boltArmHoldDuration = 0.04f;
    [SerializeField, Range(0f, 1.5f)] private float boltLaunchArcHeightTiles = 0.35f;
    [SerializeField] private bool boltMatchStepDuration = true;
    [SerializeField, Range(1f, 18f)] private float boltTravelSpeedTilesPerSecond = 8f;
    [SerializeField, Range(0.2f, 0.9f)] private float reflectedBoltOutboundStepFraction = 0.58f;
    [SerializeField, Range(0.1f, 18f)] private float reflectedBoltInitialSpeedTilesPerSecond = 6f;
    [SerializeField, Range(0.1f, 80f)] private float reflectedBoltAccelerationTilesPerSecondSquared = 30f;
    [SerializeField, Range(0.1f, 30f)] private float reflectedBoltMaxSpeedTilesPerSecond = 18f;
    [SerializeField, Range(0.05f, 0.35f)] private float reflectedBoltParryFlashDuration = 0.16f;
    [SerializeField] private float boltTrailIntensity = 36f;
    [SerializeField] private float boltImpactFlashDuration = 0.2f;
    [SerializeField] private float boltOutOfRangeFadeDuration = 0.22f;
    [SerializeField] private float boltGlowScale = 0.78f;
    [SerializeField] private float boltRayScale = 1.15f;
    [SerializeField, Range(0.05f, 0.8f)] private float boltTravelTailLocalLength = 0.42f;
    [SerializeField, Range(0.02f, 0.4f)] private float boltTravelTailLocalWidth = 0.16f;
    [SerializeField, Range(0f, 0.5f)] private float boltTravelTailBackOffset = 0.16f;
    [SerializeField, Range(0.04f, 0.25f)] private float boltTravelTailParticleLifetime = 0.12f;
    [SerializeField, Range(0f, 0.3f)] private float boltTrailParticleSpeedTiles = 0.08f;
    [SerializeField, Range(0.005f, 0.08f)] private float boltTrailParticleSizeTiles = 0.025f;
    [SerializeField] private float boltDisperseParticleCount = 18f;
    [SerializeField] private float boltStartHeight = 3f;
    [SerializeField] private float boltStartForwardOffset = 0.5f;
    [SerializeField] private float boltTravelDuration = 0.8f;
    [SerializeField] private float boltEndHeightOffset = 0f;
    [SerializeField] private AnimationCurve boltFallCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 0.05f),
        new Keyframe(0.6f, 0.38f),
        new Keyframe(0.85f, 0.82f),
        new Keyframe(1f, 1f));
    [SerializeField, Range(1.01f, 4f)] private float boltInverseCurveStrength = 1.65f;
    [SerializeField, Range(0.5f, 3f)] private float boltAccelerationExponent = 1.1f;
    [SerializeField] private float spriteAngleOffset = 0f;

    [Header("Rift FX")]
    [SerializeField] private float riftWarningGlowStrength = 0.58f;
    [SerializeField] private float riftWarningPulseSpeed = 4.2f;
    [SerializeField] private float riftBurstFlashDuration = 0.24f;
    [SerializeField] private float riftBurstRayScale = 1.45f;
    [SerializeField] private float riftResidualDuration = 0.55f;

    [Header("Shift FX")]
    [SerializeField] private float shiftDisappearFlashDuration = 0.18f;
    [SerializeField] private float shiftAppearFlashDuration = 0.22f;
    [SerializeField] private float shiftRayScale = 1.2f;
    [SerializeField] private int shiftParticleCount = 24;
    [SerializeField] private float shiftArrivalGlowDuration = 0.28f;
    [SerializeField, Range(0.1f, 0.9f)] private float shiftDisappearFraction = 0.35f;
    [SerializeField, Range(0.1f, 0.9f)] private float shiftAppearFraction = 0.55f;
    [SerializeField] private float shiftEnterYOffset = 0.8f;

    [Header("Ward FX")]
    [SerializeField] private bool useWardPrefabOnly = true;
    [SerializeField] private float wardIdleGlowStrength = 0.42f;
    [SerializeField] private float wardBlockFlashDuration = 0.24f;
    [SerializeField] private float wardBlockRayScale = 1.3f;
    [SerializeField] private float wardPulseScale = 1.28f;
    [SerializeField] private int wardSparkCount = 28;
    [SerializeField, Range(0.1f, 1.5f)] private float wardEffectTiles = 0.95f;

    [Header("Fade FX")]
    [SerializeField] private int fadeGhostCount = 4;
    [SerializeField, Range(0.05f, 0.5f)] private float fadeGhostSpacingTiles = 0.18f;
    [SerializeField] private float fadedAlpha = 0.2f;
    [SerializeField] private float fadeGhostAlpha = 0.36f;
    [SerializeField] private float fadeDissolveDuration = 0.28f;
    [SerializeField] private float fadeReappearFlashDuration = 0.2f;

    [Header("Sight FX")]
    [SerializeField] private float sightActivationFlashDuration = 0.34f;
    [SerializeField] private float sightRayScale = 1.75f;
    [SerializeField] private float sightPersistentGlowStrength = 0.42f;
    [SerializeField] private float sightAuraDuration = 0.62f;
    [SerializeField] private int sightParticleCount = 42;

    private GameObject activeRiftWarning;
    private Coroutine visualAlphaCoroutine;
    private Coroutine activeShiftRoutine;
    private float combatCellWorldDistance = 1f;
    private int combatActionRange = -1;
    private int combatActionMinRange = 0;
    private int combatGridDistance = -1;
    private Vector3 activeRiftGroundPosition;
    private bool hasActiveRiftGroundPosition;
    private Vector3 resolvedEffectGroundPosition;
    private bool hasResolvedEffectGroundPosition;
    private bool combatActionOutOfRange;
    private bool combatActionTooClose;
    private static Transform sharedWorldVfxRoot;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        CancelShift();
    }

    public void ResolveReferences()
    {
        if (visualRoot == null)
        {
            Transform named = transform.Find("visualRoot");
            if (named == null)
                named = transform.Find("VisualRoot");

            visualRoot = named != null ? named : FindAnimatorOrRendererRoot();
        }

        if (castPoint == null)
            castPoint = FindChild("CastPoint");

        if (groundVFXPoint == null)
            groundVFXPoint = FindChild("GroundVFXPoint");

        if (centerVFXPoint == null)
            centerVFXPoint = FindChild("CenterVFXPoint");

        if (vfxRoot == null)
            vfxRoot = FindChild("VFXRoot");

        if (vfxRoot == null)
        {
            GameObject root = new GameObject("VFXRoot");
            root.transform.SetParent(transform, false);
            vfxRoot = root.transform;
        }

        if (additiveSpriteMaterial == null)
            additiveSpriteMaterial = CombatShaderMaterials.OracleEnergy;

        if (boltProjectilePrefab == null)
            boltProjectilePrefab = Resources.Load<GameObject>(BoltProjectilePrefabResourcePath);
    }

    public void SetCombatPresentationContext(float cellWorldDistance, int actionRange, int gridDistance)
    {
        SetCombatPresentationContext(cellWorldDistance, actionRange, gridDistance, 0);
    }

    public void SetCombatPresentationContext(float cellWorldDistance, int actionRange, int gridDistance, int actionMinRange)
    {
        combatCellWorldDistance = Mathf.Max(0.01f, cellWorldDistance);
        combatActionRange = actionRange;
        combatActionMinRange = Mathf.Max(0, actionMinRange);
        combatGridDistance = gridDistance;
        combatActionOutOfRange = combatActionRange > 0 && combatGridDistance > combatActionRange;
        combatActionTooClose = combatActionMinRange > 0 && combatGridDistance > 0 && combatGridDistance < combatActionMinRange;
        hasResolvedEffectGroundPosition = false;
    }

    public void SetCombatPresentationContext(
        float cellWorldDistance,
        int actionRange,
        int gridDistance,
        Vector3 actorGroundPosition,
        Vector3 targetGroundPosition,
        Vector3 effectGroundPosition)
    {
        SetCombatPresentationContext(cellWorldDistance, actionRange, gridDistance, 0, actorGroundPosition, targetGroundPosition, effectGroundPosition);
    }

    public void SetCombatPresentationContext(
        float cellWorldDistance,
        int actionRange,
        int gridDistance,
        int actionMinRange,
        Vector3 actorGroundPosition,
        Vector3 targetGroundPosition,
        Vector3 effectGroundPosition)
    {
        SetCombatPresentationContext(cellWorldDistance, actionRange, gridDistance, actionMinRange);
        resolvedEffectGroundPosition = effectGroundPosition;
        hasResolvedEffectGroundPosition = true;

        if (showTileDebug)
        {
            Debug.DrawLine(actorGroundPosition, actorGroundPosition + Vector3.right * GetTileScale(1f), Color.yellow, 1f);
            Debug.DrawLine(actorGroundPosition, effectGroundPosition, Color.cyan, 1f);
            Debug.DrawLine(effectGroundPosition, targetGroundPosition, Color.magenta, 1f);
        }
    }

    public void PlayBoltCast(CharacterUnit targetUnit, float stepDuration)
    {
        PlayBoltProjectile(targetUnit, stepDuration);
    }

    public void PlayBoltProjectile(CharacterUnit targetUnit, float stepDuration)
    {
        PlayBoltProjectile(targetUnit, null, stepDuration, false);
    }

    public void PlayBoltReflectedByParry(CharacterUnit parryUnit, CharacterUnit returnTargetUnit, float stepDuration)
    {
        PlayBoltProjectile(parryUnit, returnTargetUnit, stepDuration, true);
    }

    private void PlayBoltProjectile(
        CharacterUnit targetUnit,
        CharacterUnit reflectedReturnTargetUnit,
        float stepDuration,
        bool reflectOnParry)
    {
        ResolveReferences();

        if (targetUnit == null)
            return;

        CharacterUnit casterUnit = GetComponentInParent<CharacterUnit>();
        Vector3 start = GetCastPosition();
        Vector3 casterCenter = casterUnit != null ? GetCenterPosition(casterUnit.transform) : GetCenterPosition();
        Vector3 targetCenter = GetCenterPosition(targetUnit.transform);
        Vector3 reflectedReturnTarget = reflectedReturnTargetUnit != null
            ? GetCenterPosition(reflectedReturnTargetUnit.transform)
            : casterCenter;
        Vector3 forwardDirection = GetBoltForwardDirection(casterCenter, targetCenter);

        SpawnBoltCastPolish(start, forwardDirection);
        SpawnOracleGroundGlow(GetGroundPosition(transform), stepDuration * 0.35f, GetTileScale(0.58f), oracleMagicColor);

        if (boltProjectilePrefab == null)
            return;

        GameObject effect = SpawnWorldEffect(boltProjectilePrefab, start, frontSortingOffset);
        if (effect == null)
            return;

        FaceEffect(effect.transform, forwardDirection);
        SetAnimatorDuration(effect, stepDuration);

        BoltProjectile projectile = effect.GetComponent<BoltProjectile>();
        if (projectile == null)
            projectile = effect.AddComponent<BoltProjectile>();

        if (projectile == null)
        {
            Vector3 target = GetBoltTravelTarget(start, targetCenter, out bool outOfRange);
            PlayConfiguredParticleEffect(effect, stepDuration, target - start, frontSortingOffset);
            AddBoltTravelPolish(effect, forwardDirection, stepDuration);
            StartCoroutine(MoveAndDestroy(effect, start, target, stepDuration, outOfRange, forwardDirection));
            return;
        }

        projectile.Armed += armedProjectile =>
        {
            if (armedProjectile == null)
                return;

            PlayConfiguredParticleEffect(effect, stepDuration, armedProjectile.ForwardDirection, frontSortingOffset);
            AddBoltTravelPolish(effect, armedProjectile.ForwardDirection, stepDuration);
            SpawnRing(effect.transform.position, Mathf.Max(0.08f, boltArmHoldDuration + 0.08f), GetTileScale(0.16f), GetTileScale(0.42f), new Color(0.58f, 0.96f, 1f, 0.68f), frontSortingOffset + 2);
            SpawnLightPulse(effect.transform.position, oracleMagicColor, 0.72f, GetTileScale(0.42f), GetTileScale(0.16f), Mathf.Max(0.08f, boltArmHoldDuration + 0.08f));
        };

        projectile.Finished += (finishedProjectile, reason) =>
        {
            if (finishedProjectile == null || reason != BoltProjectileFinishReason.Expired)
                return;

            SpawnBoltDispersePolish(finishedProjectile.transform.position, finishedProjectile.ForwardDirection);
        };

        if (reflectOnParry)
        {
            projectile.Reflected += (reflectedProjectile, contactPosition) =>
            {
                SpawnBoltParryDeflectPolish(contactPosition);
            };

            projectile.ReflectedImpact += (reflectedProjectile, impactPosition) =>
            {
                PlayBoltHit(impactPosition, Mathf.Max(boltImpactFlashDuration, reflectedBoltParryFlashDuration));
            };
        }

        int maxRange = combatActionRange > 0 ? combatActionRange : 4;
        int minRange = combatActionMinRange > 0 ? combatActionMinRange : 2;
        float normalizedDuration = NormalizeDuration(stepDuration);
        float launchSeconds = Mathf.Min(Mathf.Max(0.01f, boltLaunchDuration), normalizedDuration * 0.45f);
        float travelSpeedWorld = boltMatchStepDuration ? -1f : GetTileScale(boltTravelSpeedTilesPerSecond);
        float boltTravelDurationSeconds = boltMatchStepDuration ? -1f : boltTravelDuration;
        if (reflectOnParry)
            boltTravelDurationSeconds = ResolveReflectedBoltOutboundTravelDuration(normalizedDuration, launchSeconds);

        projectile.InitializePresentation(new BoltProjectile.RuntimeConfig
        {
            CasterUnit = casterUnit,
            TargetUnit = targetUnit,
            SpawnPosition = start,
            ForwardDirection = forwardDirection,
            TileWorldSize = combatCellWorldDistance,
            Damage = 0,
            MinHitRange = minRange,
            MaxHitRange = maxRange,
            GridDistanceOverride = combatGridDistance,
            TotalStepDurationSeconds = normalizedDuration,
            LaunchDurationSeconds = launchSeconds,
            ArmHoldDurationSeconds = boltArmHoldDuration,
            LaunchArcHeightWorld = GetTileScale(boltLaunchArcHeightTiles),
            TravelSpeedWorldUnitsPerSecond = travelSpeedWorld,
            BoltStartHeightWorld = boltStartHeight,
            BoltStartForwardOffsetWorld = boltStartForwardOffset,
            BoltTravelDurationSeconds = boltTravelDurationSeconds,
            BoltEndHeightOffsetWorld = boltEndHeightOffset,
            BoltFallCurve = boltFallCurve,
            BoltInverseCurveStrength = boltInverseCurveStrength,
            BoltAccelerationExponent = boltAccelerationExponent,
            SpriteAngleOffset = spriteAngleOffset,
            DefenderState = BoltDefenderState.Normal,
            ApplyStandaloneDamage = false,
            ReflectOnTargetImpact = reflectOnParry,
            ReflectedReturnTargetPosition = reflectedReturnTarget,
            ReflectedInitialSpeedWorldUnitsPerSecond = GetTileScale(reflectedBoltInitialSpeedTilesPerSecond),
            ReflectedAccelerationWorldUnitsPerSecondSquared = GetTileScale(reflectedBoltAccelerationTilesPerSecondSquared),
            ReflectedMaxSpeedWorldUnitsPerSecond = GetTileScale(reflectedBoltMaxSpeedTilesPerSecond)
        });
    }

    public void PlayBoltHit(CharacterUnit targetUnit, float duration)
    {
        if (targetUnit == null)
            return;

        PlayBoltHit(GetCenterPosition(targetUnit.transform), duration);
    }

    public void PlayBoltHit(Vector3 worldPosition, float duration)
    {
        GameObject effect = SpawnTemporaryWorldEffect(boltHitPrefab, worldPosition, duration, frontSortingOffset);
        PlayConfiguredParticleEffect(effect, duration, GetFacingDirection(), frontSortingOffset);
        SpawnBoltImpactPolish(worldPosition, duration);
    }

    public void PlayRiftWarning(CharacterUnit targetUnit, float stepDuration)
    {
        if (targetUnit == null)
            return;

        StopRiftWarning(false);

        Vector3 position = GetResolvedEffectGroundPosition(targetUnit);
        activeRiftGroundPosition = position;
        hasActiveRiftGroundPosition = true;
        LogVfx($"Rift warning WorldPos={position} OutOfRange={combatActionOutOfRange}");

        activeRiftWarning = riftWarningPrefab != null
            ? SpawnWorldEffect(riftWarningPrefab, position, groundSortingOffset, PrefabRootAnchorName)
            : CreateRuntimeRoot("Oracle_RiftWarningRuntime", position);

        SetAnimatorDuration(activeRiftWarning, stepDuration);
        DecorateRiftWarning(activeRiftWarning, position, stepDuration);
    }

    public void PlayRiftBurst(CharacterUnit targetUnit, float stepDuration)
    {
        if (targetUnit == null)
            return;

        Vector3 position = hasActiveRiftGroundPosition
            ? activeRiftGroundPosition
            : GetResolvedEffectGroundPosition(targetUnit);

        StopRiftWarning(false);
        LogVfx($"Rift burst WorldPos={position}");
        GameObject effect = SpawnTemporaryWorldEffect(riftBurstPrefab, position, stepDuration, frontSortingOffset, PrefabRootAnchorName);
        PlayConfiguredParticleEffect(effect, stepDuration, Vector3.up, frontSortingOffset);
        SpawnRiftBurstPolish(position, stepDuration);
    }

    public void StopRiftWarning(bool fadeOut)
    {
        if (activeRiftWarning == null)
            return;

        GameObject warning = activeRiftWarning;
        activeRiftWarning = null;
        hasActiveRiftGroundPosition = false;

        if (fadeOut)
            StartCoroutine(FadeAndDestroy(warning, 0.18f));
        else
            Destroy(warning);
    }

    public void PlayShiftDisappear(float stepDuration)
    {
        ResolveReferences();
        SpawnShiftDisappearPolish(GetCenterPosition(), GetGroundPosition(transform), stepDuration);
        GameObject effect = SpawnTemporaryWorldEffect(GetShiftExitPrefab(), GetVisualRootPosition(), stepDuration * 0.5f, frontSortingOffset, PrefabVisualRootAnchorName);
        PlayConfiguredParticleEffect(effect, stepDuration * 0.5f, Vector3.up, frontSortingOffset);
        StartVisualAlphaFade(GetCurrentVisualAlpha(), fadedAlpha, stepDuration * shiftDisappearFraction);
    }

    public void PlayShiftAppear(float stepDuration)
    {
        ResolveReferences();
        Vector3 center = GetCenterPosition();
        Vector3 ground = GetGroundPosition(transform);
        Vector3 enterPosition = GetShiftEnterPosition(ground);
        GameObject effect = SpawnTemporaryWorldEffect(GetShiftEnterPrefab(), enterPosition, stepDuration * 0.5f, frontSortingOffset);
        PlayConfiguredParticleEffect(effect, stepDuration * 0.5f, Vector3.up, frontSortingOffset);
        SpawnShiftAppearPolish(center, ground, stepDuration);
        StartVisualAlphaFade(GetCurrentVisualAlpha(), 1f, stepDuration * (1f - shiftAppearFraction));
    }

    public void PlayShiftSwap(float stepDuration)
    {
        ResolveReferences();
        CancelShift();
        activeShiftRoutine = StartCoroutine(PlayShiftRoutine(stepDuration));
    }

    public void CancelShift()
    {
        if (activeShiftRoutine != null)
        {
            StopCoroutine(activeShiftRoutine);
            activeShiftRoutine = null;
        }

        if (visualAlphaCoroutine != null)
        {
            StopCoroutine(visualAlphaCoroutine);
            visualAlphaCoroutine = null;
        }

        RestoreVisualAlpha();
    }

    public void PlayWard(float stepDuration)
    {
        ResolveReferences();
        Transform anchor = GetVisualRootAnchor();
        LogVfx($"Ward VisualRootPos={anchor.position}");
        GameObject effect = SpawnTemporaryWorldEffect(wardVFXPrefab, anchor.position, stepDuration, frontSortingOffset, PrefabRootAnchorName);
        activeWardEffect = effect;
        PlayConfiguredParticleEffect(effect, stepDuration, GetFacingDirection(), frontSortingOffset);
        if (!useWardPrefabOnly)
            SpawnWardIdlePolish(anchor, stepDuration);
    }

    public void PlayWardSuccess(float duration)
    {
        PlayWardSuccess(duration, GetCenterPosition() + GetFacingDirection() * GetTileScale(2f));
    }

    private GameObject activeWardEffect;
    public void PlayWardSuccess(float duration, Vector3 attackerPosition)
    {
        ResolveReferences();
        Vector3 center = GetVisualRootPosition();
        LogVfx($"Ward success VisualRootPos={center}");
        GameObject effect = SpawnTemporaryWorldEffect(wardSuccessPrefab, center, duration, frontSortingOffset + 1, PrefabRootAnchorName);
        Vector3 direction = attackerPosition.x < GetCenterPosition().x ? Vector3.left : Vector3.right;
        Vector3 contact = GetCenterPosition() + direction * GetTileScale(0.25f);
        if (activeWardEffect != null)
            activeWardEffect.GetComponent<CombatEffectShaderDriver>()?.PulseImpact(contact);
        if (effect != null)
            effect.GetComponent<CombatEffectShaderDriver>()?.PulseImpact(contact);
        PlayConfiguredParticleEffect(effect, duration, GetFacingDirection(), frontSortingOffset + 1);
        if (!useWardPrefabOnly)
            SpawnWardBlockPolish(center, duration);
    }

    public void PlayFade(float stepDuration)
    {
        ResolveReferences();
        StartCoroutine(PlayFadeRoutine(stepDuration));
    }

    public void PlayFadeSuccess(float duration)
    {
        PlayFade(duration);
    }

    public void PlaySightActivation(float stepDuration)
    {
        ResolveReferences();
        Vector3 center = GetCenterPosition();
        GameObject effect = SpawnTemporaryWorldEffect(sightActivationPrefab, center, stepDuration, frontSortingOffset + 1);
        PlayConfiguredParticleEffect(effect, stepDuration, Vector3.up, frontSortingOffset + 1);
        SpawnSightActivationPolish(center, GetGroundPosition(transform), stepDuration);
        StartCoroutine(PulseVisualRoot(stepDuration));
    }

    private IEnumerator PlayShiftRoutine(float stepDuration)
    {
        float duration = NormalizeDuration(stepDuration);
        float disappearDuration = Mathf.Clamp(duration * shiftDisappearFraction, minimumEffectDuration, duration);
        float appearDelay = Mathf.Clamp(duration * shiftAppearFraction, disappearDuration, duration);
        Vector3 departure = GetCenterPosition();
        Vector3 departureVisualRoot = GetVisualRootPosition();

        LogVfx($"Shift disappear OldPos={departure}");
        SpawnShiftDisappearPolish(departure, GetGroundPosition(transform), duration);
        GameObject disappearEffect = SpawnTemporaryWorldEffect(GetShiftExitPrefab(), departureVisualRoot, duration * 0.5f, frontSortingOffset, PrefabVisualRootAnchorName);
        PlayConfiguredParticleEffect(disappearEffect, duration * 0.5f, Vector3.up, frontSortingOffset);
        yield return FadeVisualRoot(GetCurrentVisualAlpha(), fadedAlpha, disappearDuration);

        float waitForAppear = Mathf.Max(0f, appearDelay - disappearDuration);
        if (waitForAppear > 0f)
            yield return WaitUnscaled(waitForAppear);

        Vector3 arrival = GetCenterPosition();
        Vector3 arrivalGround = GetGroundPosition(transform);
        LogVfx($"Shift appear NewPos={arrival}");
        SpawnTeleportStreak(departure, arrival, duration);
        GameObject appearEffect = SpawnTemporaryWorldEffect(GetShiftEnterPrefab(), GetShiftEnterPosition(arrivalGround), duration * 0.5f, frontSortingOffset);
        PlayConfiguredParticleEffect(appearEffect, duration * 0.5f, Vector3.up, frontSortingOffset);
        SpawnShiftAppearPolish(arrival, arrivalGround, duration);
        yield return FadeVisualRoot(GetCurrentVisualAlpha(), 1f, Mathf.Max(minimumEffectDuration, duration - appearDelay));
        activeShiftRoutine = null;
    }

    private IEnumerator PlayFadeRoutine(float stepDuration)
    {
        float duration = NormalizeDuration(stepDuration);
        Vector3 center = GetCenterPosition();
        Vector3 visualRootPosition = GetVisualRootPosition();

        LogVfx($"Fade start VisualRoot={(visualRoot != null ? visualRoot.name : name)} VisualRootPos={visualRootPosition}");
        CreateFadeAfterimages(duration);
        SpawnDirectionalStreaks(center, Vector3.left, Mathf.Max(2, fadeGhostCount), fadeDissolveDuration, GetTileScale(0.42f), new Color(0.45f, 0.85f, 1f, 0.42f));
        GameObject effect = SpawnTemporaryWorldEffect(fadeDissolvePrefab, visualRootPosition, duration, frontSortingOffset, PrefabRootAnchorName);
        PlayConfiguredParticleEffect(effect, duration, Vector3.up, frontSortingOffset);

        yield return FadeVisualRoot(GetCurrentVisualAlpha(), fadedAlpha, duration * 0.25f);
        yield return WaitUnscaled(duration * 0.25f);

        Vector3 reappearCenter = GetCenterPosition();
        LogVfx($"Fade reappear CenterPos={reappearCenter}");
        SpawnGlow(reappearCenter, fadeReappearFlashDuration, GetTileScale(0.28f), GetTileScale(0.85f), new Color(0.55f, 0.95f, 1f, 0.62f), highSortingOffset);
        SpawnRayBurst(reappearCenter, fadeReappearFlashDuration, 8, GetTileScale(0.58f), GetTileScale(0.08f), new Color(0.42f, 0.92f, 1f, 0.5f), highSortingOffset);
        SpawnLightPulse(reappearCenter, oracleMagicColor, 0.7f, GetTileScale(0.85f), GetTileScale(0.22f), fadeReappearFlashDuration);

        yield return FadeVisualRoot(GetCurrentVisualAlpha(), 1f, duration * 0.5f);
    }

    private IEnumerator PulseVisualRoot(float stepDuration)
    {
        Transform target = visualRoot != null ? visualRoot : transform;
        Vector3 startScale = target.localScale;
        Vector3 peakScale = startScale * 1.08f;
        float halfDuration = NormalizeDuration(stepDuration) * 0.5f;

        yield return ScaleTransform(target, startScale, peakScale, halfDuration);
        yield return ScaleTransform(target, peakScale, startScale, halfDuration);
    }

    private void CreateFadeAfterimages(float duration)
    {
        SpriteRenderer[] renderers = GetVisualSpriteRenderers();
        if (renderers == null || renderers.Length == 0)
            return;

        int count = Mathf.Clamp(fadeGhostCount, 1, 8);
        float spacingLimit = GetTileScale(fadeGhostSpacingTiles);
        float spacing = Mathf.Max(GetTileScale(0.02f), spacingLimit);

        for (int offsetIndex = 0; offsetIndex < count; offsetIndex++)
        {
            float side = offsetIndex % 2 == 0 ? -1f : 1f;
            float distance = ((offsetIndex / 2) + 1) * spacing;
            Vector3 retreatDirection = Vector3.right * side;
            Vector3 offset = retreatDirection * distance + Vector3.up * GetTileScale(0.08f + 0.02f * offsetIndex);

            GameObject ghostRoot = new GameObject("Oracle_FadeGhost");
            ghostRoot.transform.SetParent(GetWorldVfxRoot(), true);
            ghostRoot.transform.position = GetCenterPosition() + offset;
            ghostRoot.transform.rotation = Quaternion.identity;
            ghostRoot.transform.localScale = Vector3.one;

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer source = renderers[i];
                if (source == null || source.sprite == null)
                    continue;

                CombatGhostSnapshot.Create(source, ghostRoot.transform, offset,
                    new Color(0.45f, 0.85f, 1f, fadeGhostAlpha), duration, frontSortingOffset);
            }

            SpawnGlow(ghostRoot.transform.position, duration * 0.7f, GetTileScale(0.18f), GetTileScale(0.32f), new Color(0.38f, 0.68f, 1f, fadeGhostAlpha * 0.75f), frontSortingOffset, ghostRoot.transform);
            ghostRoot.AddComponent<CombatEffectShaderDriver>().Play(duration);
            StartCoroutine(FadeAndDestroy(ghostRoot, duration));
        }
    }

    private void SpawnBoltCastPolish(Vector3 position, Vector3 direction)
    {
        SpawnGlow(position, boltCastFlashDuration, GetTileScale(0.12f), GetTileScale(0.34f), new Color(0.45f, 0.95f, 1f, 0.88f), highSortingOffset);
        SpawnDirectionalRays(position, direction, boltCastFlashDuration, 5, GetTileScale(0.38f * boltRayScale), GetTileScale(0.05f), new Color(0.58f, 0.95f, 1f, 0.74f), highSortingOffset);
        SpawnParticleBurst(position, "Oracle_BoltCastParticles", oracleMagicColor, 14, GetTileScale(0.25f), GetTileScale(0.04f), 0.18f, true, highSortingOffset);
        SpawnLightPulse(position, oracleMagicColor, 0.55f, GetTileScale(0.65f), GetTileScale(0.18f), boltCastFlashDuration);
    }

    private void AddBoltTravelPolish(GameObject effect, Vector3 direction, float stepDuration)
    {
        if (effect == null)
            return;

        bool followProjectileRotation = effect.GetComponent<BoltProjectile>() != null;
        Vector3 safeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
        Quaternion tailRotation = followProjectileRotation
            ? Quaternion.identity
            : Quaternion.Euler(0f, 0f, Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg);
        Vector3 tailOffset = tailRotation * new Vector3(-Mathf.Max(0f, boltTravelTailBackOffset), 0f, 0f);

        GameObject glow = CreateSpriteObject("Oracle_BoltTravelGlow", RuntimeMagicSpriteLibrary.SoftCircle, effect.transform.position, effect.transform, frontSortingOffset + 1);
        glow.transform.localRotation = Quaternion.identity;
        Color glowColor = new Color(0.35f, 0.95f, 1f, 0.58f);
        glow.GetComponent<TemporaryMagicSpriteEffect>().Initialize(
            Vector3.one * boltGlowScale,
            Vector3.one * boltGlowScale,
            glowColor,
            glowColor,
            stepDuration,
            0f,
            false);

        GameObject streak = CreateSpriteObject("Oracle_BoltTravelStreak", RuntimeMagicSpriteLibrary.SoftRay, effect.transform.position, effect.transform, frontSortingOffset);
        streak.transform.localRotation = tailRotation;
        streak.transform.localPosition = tailOffset;
        Color streakColor = new Color(0.55f, 0.92f, 1f, 0.46f);
        streak.GetComponent<TemporaryMagicSpriteEffect>().Initialize(
            new Vector3(Mathf.Max(0.01f, boltTravelTailLocalLength), Mathf.Max(0.01f, boltTravelTailLocalWidth), 1f),
            new Vector3(Mathf.Max(0.01f, boltTravelTailLocalLength), Mathf.Max(0.01f, boltTravelTailLocalWidth), 1f),
            streakColor,
            streakColor,
            stepDuration,
            0f,
            false);

        ParticleSystem particles = CreateParticleBurst(effect.transform.position, "Oracle_BoltTrailParticles", new Color(0.5f, 0.9f, 1f, 0.78f), 0, GetTileScale(boltTrailParticleSpeedTiles), GetTileScale(boltTrailParticleSizeTiles), stepDuration, false, frontSortingOffset);
        if (particles != null)
        {
            particles.transform.SetParent(effect.transform, false);
            particles.transform.localPosition = Vector3.zero;
            particles.transform.localRotation = tailRotation;
            ParticleSystem.MainModule main = particles.main;
            main.duration = Mathf.Max(0.05f, stepDuration);
            main.startLifetime = Mathf.Clamp(boltTravelTailParticleLifetime, 0.04f, Mathf.Max(0.04f, stepDuration));
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = Mathf.Max(0f, boltTrailIntensity);
            particles.Play(true);
        }
    }

    private void SpawnBoltImpactPolish(Vector3 position, float duration)
    {
        float flashDuration = Mathf.Max(boltImpactFlashDuration, duration * 0.35f);
        SpawnGlow(position, flashDuration, GetTileScale(0.18f), GetTileScale(0.58f), new Color(0.7f, 0.98f, 1f, 0.86f), highSortingOffset);
        SpawnRing(position, flashDuration, GetTileScale(0.22f), GetTileScale(0.68f), new Color(0.55f, 0.9f, 1f, 0.58f), frontSortingOffset + 1);
        SpawnRayBurst(position, flashDuration, 10, GetTileScale(0.55f), GetTileScale(0.06f), new Color(0.58f, 0.96f, 1f, 0.72f), highSortingOffset);
        SpawnParticleBurst(position, "Oracle_BoltImpactParticles", oracleMagicColor, 24, GetTileScale(0.38f), GetTileScale(0.04f), flashDuration, true, highSortingOffset);
        SpawnLightPulse(position, oracleMagicColor, 0.9f, GetTileScale(0.75f), GetTileScale(0.25f), flashDuration);
    }

    private void SpawnBoltParryDeflectPolish(Vector3 position)
    {
        float duration = Mathf.Max(0.05f, reflectedBoltParryFlashDuration);
        SpawnRing(position, duration, GetTileScale(0.16f), GetTileScale(0.48f), new Color(0.8f, 0.96f, 1f, 0.72f), highSortingOffset);
        SpawnRayBurst(position, duration, 8, GetTileScale(0.38f), GetTileScale(0.045f), new Color(0.62f, 0.92f, 1f, 0.68f), highSortingOffset);
        SpawnParticleBurst(position, "Oracle_BoltParryDeflectParticles", oracleMagicColor, 12, GetTileScale(0.28f), GetTileScale(0.03f), duration, true, highSortingOffset);
        SpawnLightPulse(position, oracleMagicColor, 0.55f, GetTileScale(0.52f), GetTileScale(0.16f), duration);
    }

    private void SpawnBoltDispersePolish(Vector3 position, Vector3 direction)
    {
        SpawnGlow(position, boltOutOfRangeFadeDuration, GetTileScale(0.28f), GetTileScale(0.08f), new Color(0.45f, 0.9f, 1f, 0.5f), frontSortingOffset);
        SpawnDirectionalRays(position, -direction, boltOutOfRangeFadeDuration, 4, GetTileScale(0.38f), GetTileScale(0.045f), new Color(0.52f, 0.82f, 1f, 0.4f), frontSortingOffset);
        SpawnParticleBurst(position, "Oracle_BoltDisperseParticles", new Color(0.45f, 0.85f, 1f, 0.62f), Mathf.RoundToInt(boltDisperseParticleCount), GetTileScale(0.24f), GetTileScale(0.035f), boltOutOfRangeFadeDuration, true, frontSortingOffset);
    }

    private void DecorateRiftWarning(GameObject warning, Vector3 position, float stepDuration)
    {
        if (warning == null)
            return;

        SpawnGlow(position, stepDuration, GetTileScale(0.28f), GetTileScale(0.58f), new Color(0.58f, 0.26f, 1f, riftWarningGlowStrength), groundSortingOffset, warning.transform);
        SpawnRing(position, stepDuration * 0.65f, GetTileScale(0.32f), GetTileScale(0.66f), new Color(0.55f, 0.32f, 1f, 0.55f), groundSortingOffset + 1, warning.transform);
        SpawnRayBurst(position, stepDuration * 0.5f, 8, GetTileScale(0.42f), GetTileScale(0.045f), new Color(0.65f, 0.38f, 1f, 0.5f), groundSortingOffset + 1, warning.transform);
        SpawnParticleBurst(position + Vector3.up * GetTileScale(0.08f), "Oracle_RiftWarningDrift", oracleDeepMagicColor, 18, GetTileScale(0.16f), GetTileScale(0.025f), stepDuration, true, frontSortingOffset);
        StartCoroutine(RiftWarningPulseRoutine(warning.transform, position, stepDuration));
    }

    private IEnumerator RiftWarningPulseRoutine(Transform owner, Vector3 position, float duration)
    {
        float elapsed = 0f;
        float interval = 1f / Mathf.Max(0.01f, riftWarningPulseSpeed);

        while (owner != null && elapsed < duration)
        {
            SpawnRing(position, interval * 0.9f, GetTileScale(0.28f), GetTileScale(0.62f), new Color(0.75f, 0.45f, 1f, 0.36f), groundSortingOffset + 2, owner);
            yield return WaitUnscaled(interval);
            elapsed += interval;
        }
    }

    private void SpawnRiftBurstPolish(Vector3 position, float stepDuration)
    {
        SpawnGlow(position, riftBurstFlashDuration, GetTileScale(0.32f), GetTileScale(0.78f), new Color(0.72f, 0.42f, 1f, 0.86f), highSortingOffset);
        SpawnRing(position, riftResidualDuration, GetTileScale(0.34f), GetTileScale(0.74f), new Color(0.45f, 0.18f, 1f, 0.52f), groundSortingOffset + 2);
        SpawnRayBurst(position, riftBurstFlashDuration, 14, GetTileScale(0.44f * riftBurstRayScale), GetTileScale(0.06f), new Color(0.7f, 0.48f, 1f, 0.78f), highSortingOffset);
        SpawnVerticalBurst(position, riftBurstFlashDuration, 4, GetTileScale(0.62f), new Color(0.8f, 0.5f, 1f, 0.58f), highSortingOffset);
        SpawnParticleBurst(position, "Oracle_RiftBurstParticles", oracleDeepMagicColor, 36, GetTileScale(0.48f), GetTileScale(0.04f), riftResidualDuration, true, highSortingOffset);
        SpawnLightPulse(position, oracleDeepMagicColor, 1.0f, GetTileScale(0.85f), GetTileScale(0.28f), riftBurstFlashDuration);
    }

    private void SpawnShiftDisappearPolish(Vector3 center, Vector3 ground, float duration)
    {
        SpawnGlow(center, shiftDisappearFlashDuration, GetTileScale(0.2f), GetTileScale(0.58f), new Color(0.62f, 0.82f, 1f, 0.75f), highSortingOffset);
        SpawnRing(ground, shiftDisappearFlashDuration, GetTileScale(0.22f), GetTileScale(0.52f), new Color(0.46f, 0.38f, 1f, 0.52f), groundSortingOffset + 2);
        SpawnRayBurst(center, shiftDisappearFlashDuration, 12, GetTileScale(0.38f * shiftRayScale), GetTileScale(0.05f), new Color(0.58f, 0.72f, 1f, 0.62f), highSortingOffset);
        SpawnParticleBurst(center, "Oracle_ShiftDisappearParticles", oracleMagicColor, shiftParticleCount, GetTileScale(0.34f), GetTileScale(0.03f), shiftDisappearFlashDuration, true, highSortingOffset);
        SpawnLightPulse(center, oracleMagicColor, 0.75f, GetTileScale(0.72f), GetTileScale(0.2f), shiftDisappearFlashDuration);
    }

    private void SpawnShiftAppearPolish(Vector3 center, Vector3 ground, float duration)
    {
        SpawnGlow(center, shiftAppearFlashDuration, GetTileScale(0.2f), GetTileScale(0.62f), new Color(0.62f, 0.96f, 1f, 0.82f), highSortingOffset);
        SpawnRing(ground, shiftArrivalGlowDuration, GetTileScale(0.22f), GetTileScale(0.58f), new Color(0.45f, 0.62f, 1f, 0.6f), groundSortingOffset + 2);
        SpawnRayBurst(center, shiftAppearFlashDuration, 12, GetTileScale(0.4f * shiftRayScale), GetTileScale(0.05f), new Color(0.58f, 0.94f, 1f, 0.72f), highSortingOffset);
        SpawnParticleBurst(center, "Oracle_ShiftAppearParticles", oracleMagicColor, shiftParticleCount, GetTileScale(0.34f), GetTileScale(0.035f), shiftAppearFlashDuration, true, highSortingOffset);
        SpawnLightPulse(center, oracleMagicColor, 0.8f, GetTileScale(0.75f), GetTileScale(0.22f), shiftAppearFlashDuration);
    }

    private void SpawnTeleportStreak(Vector3 from, Vector3 to, float duration)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.16f)
            return;

        Vector3 midpoint = (from + to) * 0.5f;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float length = Mathf.Clamp(delta.magnitude, GetTileScale(0.25f), GetWorldDistanceForRange(1));
        GameObject streak = CreateSpriteObject("Oracle_ShiftTransitStreak", RuntimeMagicSpriteLibrary.SoftRay, midpoint, null, frontSortingOffset);
        streak.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        TemporaryMagicSpriteEffect fx = streak.GetComponent<TemporaryMagicSpriteEffect>();
        fx.Initialize(
            new Vector3(length * 1.1f, 0.12f, 1f),
            new Vector3(length * 0.65f, 0.02f, 1f),
            new Color(0.45f, 0.8f, 1f, 0.42f),
            new Color(0.45f, 0.8f, 1f, 0f),
            Mathf.Min(0.18f, duration * 0.25f),
            0f,
            true);
    }

    private void SpawnWardIdlePolish(Transform anchor, float duration)
    {
        if (anchor == null)
            return;

        SpawnGlow(anchor.position, duration * 0.7f, GetTileScale(0.34f), Mathf.Min(GetWardEffectScale(), GetTileScale(0.7f * wardPulseScale)), new Color(0.35f, 0.86f, 1f, wardIdleGlowStrength), frontSortingOffset, anchor);
        SpawnRayBurst(anchor.position, duration * 0.45f, 10, GetTileScale(0.44f), GetTileScale(0.04f), new Color(0.58f, 0.96f, 1f, 0.38f), frontSortingOffset + 1, anchor);
        SpawnLightPulse(anchor.position, oracleMagicColor, 0.5f, GetTileScale(0.72f), GetTileScale(0.12f), Mathf.Min(0.25f, duration * 0.3f));
    }

    private void SpawnWardBlockPolish(Vector3 center, float duration)
    {
        float flashDuration = Mathf.Max(wardBlockFlashDuration, duration * 0.35f);
        SpawnGlow(center, flashDuration, GetTileScale(0.36f), GetWardEffectScale(), new Color(0.62f, 0.98f, 1f, 0.88f), highSortingOffset);
        SpawnRing(center, flashDuration, GetTileScale(0.32f), GetWardEffectScale(), new Color(0.55f, 0.95f, 1f, 0.7f), highSortingOffset);
        SpawnRayBurst(center, flashDuration, 16, GetTileScale(0.45f * wardBlockRayScale), GetTileScale(0.055f), new Color(0.72f, 0.98f, 1f, 0.78f), highSortingOffset);
        SpawnParticleBurst(center, "Oracle_WardBlockSparks", oracleGoldMagicColor, wardSparkCount, GetTileScale(0.52f), GetTileScale(0.035f), flashDuration, true, highSortingOffset);
        SpawnLightPulse(center, oracleMagicColor, 0.9f, GetTileScale(0.9f), GetTileScale(0.2f), flashDuration);
    }

    private void SpawnSightActivationPolish(Vector3 center, Vector3 ground, float stepDuration)
    {
        SpawnGlow(center, sightActivationFlashDuration, GetTileScale(0.35f), GetTileScale(0.9f), new Color(0.82f, 0.92f, 1f, 0.95f), highSortingOffset);
        SpawnGlow(center, sightAuraDuration, GetTileScale(0.52f), GetTileScale(1.0f), new Color(0.5f, 0.35f, 1f, sightPersistentGlowStrength), frontSortingOffset);
        SpawnRing(center, sightActivationFlashDuration, GetTileScale(0.35f), GetTileScale(0.82f), new Color(0.85f, 0.65f, 1f, 0.72f), highSortingOffset);
        SpawnRing(ground, sightAuraDuration, GetTileScale(0.3f), GetTileScale(0.68f), new Color(0.62f, 0.45f, 1f, 0.42f), groundSortingOffset + 2);
        SpawnRayBurst(center, sightActivationFlashDuration, 20, GetTileScale(0.41f * sightRayScale), GetTileScale(0.06f), new Color(0.86f, 0.92f, 1f, 0.82f), highSortingOffset);
        SpawnParticleBurst(center + Vector3.up * GetTileScale(0.08f), "Oracle_SightAwakeningParticles", new Color(0.7f, 0.55f, 1f, 0.8f), sightParticleCount, GetTileScale(0.45f), GetTileScale(0.035f), sightAuraDuration, true, highSortingOffset);
        SpawnLightPulse(center, new Color(0.6f, 0.55f, 1f, 1f), 1.0f, GetTileScale(1.0f), GetTileScale(0.25f), sightActivationFlashDuration);
    }

    private void SpawnOracleGroundGlow(Vector3 position, float duration, float scale, Color color)
    {
        float clampedScale = Mathf.Min(scale, GetTileScale(0.58f));
        SpawnRing(position, duration, clampedScale * 0.65f, clampedScale * 1.05f, new Color(color.r, color.g, color.b, color.a * 0.45f), groundSortingOffset + 1);
        SpawnGlow(position, duration, clampedScale * 0.45f, clampedScale * 0.85f, new Color(color.r, color.g, color.b, color.a * 0.22f), groundSortingOffset);
    }

    private void SpawnDirectionalStreaks(Vector3 position, Vector3 direction, int count, float duration, float rayScale, Color color)
    {
        SpawnDirectionalRays(position, direction, duration, count, rayScale, GetTileScale(0.05f), color, frontSortingOffset);
    }

    private void SpawnDirectionalRays(Vector3 position, Vector3 direction, float duration, int count, float length, float width, Color color, int sortingOffset)
    {
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.right * GetFacingSign();

        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float spread = 42f;

        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0.5f : (float)i / (count - 1);
            float angle = baseAngle + Mathf.Lerp(-spread, spread, t);
            SpawnRay(position, angle, duration, length * Random.Range(0.78f, 1.08f), width, color, sortingOffset + i);
        }
    }

    private void SpawnRayBurst(Vector3 position, float duration, int count, float length, float width, Color color, int sortingOffset, Transform parent = null)
    {
        float angleStep = 360f / Mathf.Max(1, count);
        float offset = Random.Range(0f, angleStep);

        for (int i = 0; i < count; i++)
        {
            float angle = offset + angleStep * i;
            SpawnRay(position, angle, duration, length * Random.Range(0.82f, 1.15f), width, color, sortingOffset + i, parent);
        }
    }

    private void SpawnVerticalBurst(Vector3 position, float duration, int count, float length, Color color, int sortingOffset)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = 72f + i * (36f / Mathf.Max(1, count - 1));
            SpawnRay(position + Vector3.up * GetTileScale(0.16f), angle, duration, length, GetTileScale(0.08f), color, sortingOffset + i);
        }
    }

    private void SpawnRay(Vector3 position, float angle, float duration, float length, float width, Color color, int sortingOffset, Transform parent = null)
    {
        GameObject ray = CreateSpriteObject("Oracle_MagicRay", RuntimeMagicSpriteLibrary.SoftRay, position, parent, sortingOffset);
        ray.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        TemporaryMagicSpriteEffect fx = ray.GetComponent<TemporaryMagicSpriteEffect>();
        fx.Initialize(
            new Vector3(length * 0.25f, width, 1f),
            new Vector3(length, width * 0.35f, 1f),
            color,
            new Color(color.r, color.g, color.b, 0f),
            duration,
            0f,
            true);
    }

    private void SpawnGlow(Vector3 position, float duration, float startScale, float endScale, Color color, int sortingOffset, Transform parent = null)
    {
        GameObject glow = CreateSpriteObject("Oracle_MagicGlow", RuntimeMagicSpriteLibrary.SoftCircle, position, parent, sortingOffset);
        TemporaryMagicSpriteEffect fx = glow.GetComponent<TemporaryMagicSpriteEffect>();
        fx.Initialize(
            Vector3.one * startScale,
            Vector3.one * endScale,
            color,
            new Color(color.r, color.g, color.b, 0f),
            duration,
            0f,
            true);
    }

    private void SpawnRing(Vector3 position, float duration, float startScale, float endScale, Color color, int sortingOffset, Transform parent = null)
    {
        GameObject ring = CreateSpriteObject("Oracle_MagicRing", RuntimeMagicSpriteLibrary.SoftRing, position, parent, sortingOffset);
        TemporaryMagicSpriteEffect fx = ring.GetComponent<TemporaryMagicSpriteEffect>();
        fx.Initialize(
            Vector3.one * startScale,
            Vector3.one * endScale,
            color,
            new Color(color.r, color.g, color.b, 0f),
            duration,
            90f,
            true);
    }

    private ParticleSystem SpawnParticleBurst(Vector3 position, string name, Color color, int count, float speed, float size, float duration, bool worldSpace, int sortingOffset)
    {
        ParticleSystem particles = CreateParticleBurst(position, name, color, count, speed, size, duration, worldSpace, sortingOffset);
        if (particles != null && count > 0)
        {
            particles.Emit(count);
            particles.Play(true);
        }

        return particles;
    }

    private ParticleSystem CreateParticleBurst(Vector3 position, string name, Color color, int count, float speed, float size, float duration, bool worldSpace, int sortingOffset)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(GetWorldVfxRoot(), true);
        obj.transform.position = position;

        ParticleSystem particles = obj.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.duration = Mathf.Max(0.05f, duration);
        main.loop = false;
        main.startLifetime = Mathf.Max(0.08f, duration);
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = GetTileScale(0.14f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(color.r, color.g, color.b), 0f),
                new GradientColorKey(new Color(color.r, color.g, color.b), 1f)
            },
            new[]
            {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (CombatShaderMaterials.OracleParticles != null)
            renderer.sharedMaterial = CombatShaderMaterials.OracleParticles;
        renderer.sortingLayerID = GetEffectSortingLayerId();
        renderer.sortingOrder = ParticleSortingOrder;

        StartCoroutine(DestroyAfterUnscaled(obj, Mathf.Max(0.1f, duration) + 1f));
        return particles;
    }

    private void SpawnLightPulse(Vector3 position, Color color, float intensity, float radius, float radiusExpansion, float duration)
    {
        if (!useTemporaryLight2DPulses)
            return;

        GameObject lightObject = new GameObject("Oracle_TemporaryLight2DPulse");
        lightObject.transform.SetParent(GetWorldVfxRoot(), true);
        lightObject.transform.position = position;

        UnityEngine.Rendering.Universal.Light2D light2D = lightObject.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        light2D.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        light2D.color = color;
        light2D.intensity = Mathf.Max(0f, intensity);
        light2D.pointLightOuterRadius = Mathf.Max(0.01f, radius);
        light2D.pointLightInnerRadius = light2D.pointLightOuterRadius * 0.25f;
        light2D.falloffIntensity = 0.6f;

        StartCoroutine(PulseLightAndDestroy(lightObject, light2D, radius, radius + radiusExpansion, duration));
    }

    private IEnumerator PulseLightAndDestroy(GameObject lightObject, UnityEngine.Rendering.Universal.Light2D light2D, float startOuterRadius, float endOuterRadius, float duration)
    {
        duration = NormalizeDuration(duration);
        startOuterRadius = Mathf.Max(0.01f, startOuterRadius);
        endOuterRadius = Mathf.Max(startOuterRadius, endOuterRadius);

        float startIntensity = light2D != null ? light2D.intensity : 0f;
        float elapsed = 0f;

        while (elapsed < duration && lightObject != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float fade = 1f - t;

            if (light2D != null)
            {
                light2D.intensity = startIntensity * fade;
                light2D.pointLightOuterRadius = Mathf.Lerp(startOuterRadius, endOuterRadius, t);
                light2D.pointLightInnerRadius = light2D.pointLightOuterRadius * 0.25f;
            }

            yield return null;
        }

        if (lightObject != null)
            Destroy(lightObject);
    }

    private GameObject CreateSpriteObject(string name, Sprite sprite, Vector3 position, Transform parent, int sortingOffset)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent != null ? parent : GetWorldVfxRoot(), true);
        obj.transform.position = position;
        obj.transform.rotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.white;
        if (additiveSpriteMaterial != null)
            renderer.sharedMaterial = additiveSpriteMaterial;

        SetRendererSorting(renderer, sortingOffset);
        obj.AddComponent<TemporaryMagicSpriteEffect>();
        obj.AddComponent<CombatEffectShaderDriver>();
        return obj;
    }

    private GameObject CreateRuntimeRoot(string name, Vector3 position)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(GetWorldVfxRoot(), true);
        obj.transform.position = position;
        obj.transform.rotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        return obj;
    }

    private GameObject SpawnTemporaryAttachedEffect(GameObject prefab, Transform parent, float duration, int sortingOffset)
    {
        if (prefab == null || parent == null)
            return null;

        GameObject effect = Instantiate(prefab, parent.position, Quaternion.identity, parent);
        effect.transform.localPosition = Vector3.zero;
        effect.transform.localRotation = Quaternion.identity;
        ApplySorting(effect, sortingOffset);
        SetAnimatorDuration(effect, duration);
        StartCoroutine(DestroyAfterUnscaled(effect, GetTemporaryEffectLifetime(effect, duration)));
        return effect;
    }

    private GameObject SpawnTemporaryWorldEffect(GameObject prefab, Vector3 position, float duration, int sortingOffset, string anchorName = null)
    {
        GameObject effect = SpawnWorldEffect(prefab, position, sortingOffset, anchorName);
        if (effect == null)
            return null;

        SetAnimatorDuration(effect, duration);
        StartCoroutine(DestroyAfterUnscaled(effect, GetTemporaryEffectLifetime(effect, duration)));
        return effect;
    }

    private GameObject SpawnWorldEffect(GameObject prefab, Vector3 position, int sortingOffset, string anchorName = null)
    {
        if (prefab == null)
            return null;

        Transform parent = GetWorldVfxRoot();
        GameObject effect = Instantiate(prefab, position, Quaternion.identity, parent);
        effect.transform.SetParent(parent, true);
        AlignEffectAnchorToWorldPosition(effect, anchorName, position);
        ApplySorting(effect, sortingOffset);
        return effect;
    }

    private void AlignEffectAnchorToWorldPosition(GameObject effect, string anchorName, Vector3 worldPosition)
    {
        if (effect == null || string.IsNullOrEmpty(anchorName))
            return;

        Transform anchor = FindEffectAnchor(effect.transform, anchorName);
        if (anchor == null)
        {
            LogVfx($"Anchor '{anchorName}' not found on {effect.name}; prefab root kept at requested position.");
            return;
        }

        effect.transform.position += worldPosition - anchor.position;
    }

    private Transform FindEffectAnchor(Transform effectRoot, string anchorName)
    {
        if (effectRoot == null || string.IsNullOrEmpty(anchorName))
            return null;

        Transform[] children = effectRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == anchorName)
                return children[i];
        }

        if (anchorName != PrefabVisualRootAnchorName)
            return null;

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == "visualRoot")
                return children[i];
        }

        return null;
    }

    private void PlayConfiguredParticleEffect(GameObject effect, float duration, Vector3 direction, int sortingOffset)
    {
        if (effect == null)
            return;

        OracleParticleEffect particleEffect = effect.GetComponent<OracleParticleEffect>();
        if (particleEffect == null)
            return;

        if (additiveSpriteMaterial == null)
            additiveSpriteMaterial = CombatShaderMaterials.OracleEnergy;

        particleEffect.PlayConfigured(NormalizeDuration(duration), direction, CombatShaderMaterials.OracleParticles, GetEffectSortingLayerId(), sortingOffset);
    }

    private float GetTemporaryEffectLifetime(GameObject effect, float duration)
    {
        duration = NormalizeDuration(duration);
        OracleParticleEffect particleEffect = effect != null ? effect.GetComponent<OracleParticleEffect>() : null;
        return particleEffect != null ? particleEffect.GetSuggestedCleanupSeconds(duration) : duration;
    }

    private IEnumerator MoveAndDestroy(GameObject effect, Vector3 start, Vector3 target, float duration, bool fadeAtEnd, Vector3 travelDirection)
    {
        if (effect == null)
            yield break;

        duration = NormalizeDuration(duration);
        float timer = 0f;

        while (timer < duration && effect != null)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / duration));
            effect.transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        if (effect != null)
        {
            effect.transform.position = target;

            if (fadeAtEnd)
            {
                SpawnBoltDispersePolish(target, travelDirection);
                yield return FadeAndDestroy(effect, boltOutOfRangeFadeDuration);
            }
            else
            {
                Destroy(effect);
            }
        }
    }

    private Vector3 GetBoltTravelTarget(Vector3 start, Vector3 targetCenter, out bool outOfRange)
    {
        outOfRange = combatActionRange > 0 && combatGridDistance > combatActionRange;
        if (!outOfRange && !combatActionTooClose)
            return targetCenter;

        Vector3 direction = GetBoltForwardDirection(start, targetCenter);
        if (direction.sqrMagnitude < 0.0001f)
            return targetCenter;

        int maxRange = combatActionRange > 0 ? combatActionRange : 4;
        float maxWorldDistance = GetWorldDistanceForRange(maxRange);
        return start + direction.normalized * Mathf.Max(0.01f, maxWorldDistance);
    }

    private float ResolveReflectedBoltOutboundTravelDuration(float stepDuration, float launchSeconds)
    {
        stepDuration = NormalizeDuration(stepDuration);
        float armSeconds = Mathf.Max(0f, boltArmHoldDuration);
        float minimumTravelSeconds = 0.05f;
        float contactTime = Mathf.Clamp(
            stepDuration * Mathf.Clamp01(reflectedBoltOutboundStepFraction),
            launchSeconds + armSeconds + minimumTravelSeconds,
            Mathf.Max(launchSeconds + armSeconds + minimumTravelSeconds, stepDuration - minimumTravelSeconds)
        );

        return Mathf.Max(minimumTravelSeconds, contactTime - launchSeconds - armSeconds);
    }

    private Vector3 GetBoltForwardDirection(Vector3 start, Vector3 targetCenter)
    {
        float deltaX = targetCenter.x - start.x;
        if (Mathf.Abs(deltaX) < 0.001f)
            return GetFacingDirection();

        return deltaX < 0f ? Vector3.left : Vector3.right;
    }

    private IEnumerator FadeAndDestroy(GameObject effect, float duration)
    {
        if (effect == null)
            yield break;

        SpriteRenderer[] renderers = effect.GetComponentsInChildren<SpriteRenderer>(true);
        ParticleSystem[] particleSystems = effect.GetComponentsInChildren<ParticleSystem>(true);
        Color[] startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            startColors[i] = renderers[i] != null ? renderers[i].color : Color.white;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] != null)
                particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        duration = NormalizeDuration(duration);
        float timer = 0f;

        while (timer < duration && effect != null)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = 1f - Mathf.Clamp01(timer / duration);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color color = startColors[i];
                color.a *= alpha;
                renderers[i].color = color;
            }

            yield return null;
        }

        if (effect != null)
            Destroy(effect);
    }

    private IEnumerator DestroyAfterUnscaled(GameObject effect, float duration)
    {
        yield return WaitUnscaled(duration);

        if (effect != null)
            Destroy(effect);
    }

    private float GetWorldDistanceForRange(int range)
    {
        return Mathf.Max(0, range) * Mathf.Max(0.01f, combatCellWorldDistance);
    }

    private float GetTileScale(float tiles)
    {
        return Mathf.Max(0.03f, Mathf.Max(0.01f, combatCellWorldDistance) * Mathf.Max(0.01f, tiles));
    }

    private float GetWardEffectScale()
    {
        return Mathf.Min(GetTileScale(wardEffectTiles), GetCharacterVisualWidthScale(1.05f));
    }

    private float GetCharacterVisualWidthScale(float fallbackTiles)
    {
        SpriteRenderer[] renderers = GetVisualSpriteRenderers();
        if (renderers == null || renderers.Length == 0)
            return GetTileScale(fallbackTiles);

        bool hasBounds = false;
        Bounds combined = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i].sprite == null)
                continue;

            if (!hasBounds)
            {
                combined = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderers[i].bounds);
            }
        }

        if (!hasBounds || combined.size.x <= 0.001f)
            return GetTileScale(fallbackTiles);

        return Mathf.Clamp(combined.size.x * 0.65f, GetTileScale(0.25f), GetTileScale(fallbackTiles));
    }

    private Transform GetWorldVfxRoot()
    {
        if (sharedWorldVfxRoot != null)
            return sharedWorldVfxRoot;

        GameObject root = GameObject.Find("OracleWorldVFXRoot");
        if (root == null)
            root = new GameObject("OracleWorldVFXRoot");

        sharedWorldVfxRoot = root.transform;
        return sharedWorldVfxRoot;
    }

    private Vector3 GetResolvedEffectGroundPosition(CharacterUnit targetUnit)
    {
        if (hasResolvedEffectGroundPosition)
            return resolvedEffectGroundPosition;

        if (targetUnit != null)
            return GetGroundPosition(targetUnit.transform);

        Debug.LogWarning("[OracleVFX] Missing resolved ground position and target; using Oracle ground position.");
        return GetGroundPosition(transform);
    }

    private void LogVfx(string message)
    {
        if (verboseVfxLogs)
            Debug.Log("[OracleVFX] " + message);
    }

    private void StartVisualAlphaFade(float fromAlpha, float toAlpha, float duration)
    {
        if (visualAlphaCoroutine != null)
            StopCoroutine(visualAlphaCoroutine);

        visualAlphaCoroutine = StartCoroutine(FadeVisualRoot(fromAlpha, toAlpha, duration));
    }

    private GameObject GetShiftExitPrefab()
    {
        return shiftExitFXPrefab != null ? shiftExitFXPrefab : shiftVFXPrefab;
    }

    private GameObject GetShiftEnterPrefab()
    {
        return shiftEnterFXPrefab != null ? shiftEnterFXPrefab : shiftVFXPrefab;
    }

    private Vector3 GetShiftEnterPosition(Vector3 basePosition)
    {
        return basePosition + Vector3.up * shiftEnterYOffset;
    }

    private void RestoreVisualAlpha()
    {
        CharacterShaderFeedback feedback = GetComponent<CharacterShaderFeedback>();
        if (feedback != null)
        {
            feedback.SetPhaseVisibility(1f);
            return;
        }
        SpriteRenderer[] renderers = GetVisualSpriteRenderers();
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color = renderers[i].color;
            color.a = 1f;
            renderers[i].color = color;
        }
    }

    private IEnumerator FadeVisualRoot(float fromAlpha, float toAlpha, float duration)
    {
        SpriteRenderer[] renderers = GetVisualSpriteRenderers();
        if (renderers == null || renderers.Length == 0)
            yield break;

        duration = NormalizeDuration(duration);
        Color[] baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i] != null ? renderers[i].color : Color.white;

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(fromAlpha, toAlpha, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / duration)));
            ApplyVisualAlpha(renderers, baseColors, alpha);
            yield return null;
        }

        ApplyVisualAlpha(renderers, baseColors, toAlpha);

        if (Mathf.Approximately(toAlpha, 1f))
            visualAlphaCoroutine = null;
    }

    private void ApplyVisualAlpha(SpriteRenderer[] renderers, Color[] baseColors, float alpha)
    {
        CharacterShaderFeedback shaderFeedback = GetComponent<CharacterShaderFeedback>();
        if (shaderFeedback != null)
        {
            shaderFeedback.SetPhaseVisibility(alpha);
            return;
        }
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || i >= baseColors.Length)
                continue;

            Color color = baseColors[i];
            color.a = alpha;
            renderers[i].color = color;
        }
    }

    private IEnumerator ScaleTransform(Transform target, Vector3 from, Vector3 to, float duration)
    {
        if (target == null)
            yield break;

        duration = NormalizeDuration(duration);
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / duration));
            target.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }

        target.localScale = to;
    }

    private IEnumerator WaitUnscaled(float duration)
    {
        duration = Mathf.Max(0f, duration);
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void SetAnimatorDuration(GameObject effect, float duration)
    {
        if (effect == null)
            return;

        duration = NormalizeDuration(duration);
        effect.GetComponent<CombatEffectShaderDriver>()?.Play(duration);
        Animator[] animators = effect.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.runtimeAnimatorController == null)
                continue;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            if (clips == null || clips.Length == 0 || clips[0] == null)
                continue;

            animator.speed = clips[0].length > 0.0001f ? clips[0].length / duration : 1f;
        }
    }

    private void ApplySorting(GameObject effect, int sortingOffset)
    {
        if (effect == null)
            return;

        SpriteRenderer[] renderers = effect.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            SetRendererSorting(renderers[i], sortingOffset + i);
        }

        ParticleSystemRenderer[] particleRenderers = effect.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < particleRenderers.Length; i++)
        {
            if (particleRenderers[i] == null)
                continue;

            particleRenderers[i].sortingLayerID = GetEffectSortingLayerId();
            particleRenderers[i].sortingOrder = ParticleSortingOrder;
        }
    }

    private void SetRendererSorting(SpriteRenderer renderer, int sortingOffset)
    {
        if (renderer == null)
            return;

        renderer.sortingLayerID = GetEffectSortingLayerId();

        SpriteRenderer ownerRenderer = GetPrimaryVisualRenderer();
        if (ownerRenderer != null)
        {
            renderer.sortingOrder = ownerRenderer.sortingOrder + sortingOffset;
        }
        else
        {
            renderer.sortingOrder = sortingOffset;
        }
    }

    private static int GetEffectSortingLayerId()
    {
        int layerId = SortingLayer.NameToID(EffectSortingLayerName);
        return layerId != 0 ? layerId : SortingLayer.NameToID(LegacyEffectSortingLayerName);
    }

    private void FaceEffect(Transform effect, Vector3 direction)
    {
        if (effect == null || direction.sqrMagnitude < 0.0001f)
            return;

        effect.localRotation = direction.x < 0f
            ? Quaternion.Euler(0f, 180f, 0f)
            : Quaternion.identity;
    }

    private SpriteRenderer[] GetVisualSpriteRenderers()
    {
        ResolveReferences();

        Transform root = visualRoot != null ? visualRoot : transform;
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        if (vfxRoot == null)
            return renderers;

        int validCount = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && !renderers[i].transform.IsChildOf(vfxRoot))
                validCount++;
        }

        SpriteRenderer[] filtered = new SpriteRenderer[validCount];
        int index = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i].transform.IsChildOf(vfxRoot))
                continue;

            filtered[index++] = renderers[i];
        }

        return filtered;
    }

    private SpriteRenderer GetPrimaryVisualRenderer()
    {
        SpriteRenderer[] renderers = GetVisualSpriteRenderers();
        if (renderers == null || renderers.Length == 0)
            return null;

        return renderers[0];
    }

    private float GetCurrentVisualAlpha()
    {
        CharacterShaderFeedback shaderFeedback = GetComponent<CharacterShaderFeedback>();
        if (shaderFeedback != null)
            return shaderFeedback.Visibility;
        SpriteRenderer renderer = GetPrimaryVisualRenderer();
        return renderer != null ? renderer.color.a : 1f;
    }

    private Vector3 GetCastPosition()
    {
        ResolveReferences();
        if (castPoint != null)
            return castPoint.position;

        return GetCenterPosition() + Vector3.right * GetFacingSign() * GetTileScale(0.25f);
    }

    private Vector3 GetCenterPosition()
    {
        ResolveReferences();
        if (centerVFXPoint != null)
            return centerVFXPoint.position;

        return GetCenterPosition(transform);
    }

    private Vector3 GetCenterPosition(Transform target)
    {
        if (target == null)
            return transform.position;

        Collider2D collider2D = target.GetComponentInChildren<Collider2D>();
        if (collider2D != null)
            return collider2D.bounds.center;

        Renderer renderer = target.GetComponentInChildren<Renderer>();
        if (renderer != null)
            return renderer.bounds.center;

        return target.position + Vector3.up * GetTileScale(0.8f);
    }

    private Vector3 GetGroundPosition(Transform target)
    {
        if (target == null)
            return transform.position;

        Collider2D collider2D = target.GetComponentInChildren<Collider2D>();
        if (collider2D != null)
            return new Vector3(collider2D.bounds.center.x, collider2D.bounds.min.y, target.position.z);

        Renderer renderer = target.GetComponentInChildren<Renderer>();
        if (renderer != null)
            return new Vector3(renderer.bounds.center.x, renderer.bounds.min.y, target.position.z);

        return target.position;
    }

    private Transform GetVisualRootAnchor()
    {
        ResolveReferences();
        return visualRoot != null ? visualRoot : transform;
    }

    private Vector3 GetVisualRootPosition()
    {
        Transform anchor = GetVisualRootAnchor();
        return anchor != null ? anchor.position : transform.position;
    }

    private Transform GetCenterAnchor()
    {
        ResolveReferences();
        return centerVFXPoint != null ? centerVFXPoint : transform;
    }

    private Vector3 GetFacingDirection()
    {
        return Vector3.right * GetFacingSign();
    }

    private Transform FindChild(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
                return children[i];
        }

        return null;
    }

    private Transform FindAnimatorOrRendererRoot()
    {
        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator != null)
            return animator.transform;

        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.transform : transform;
    }

    private float GetFacingSign()
    {
        Transform facingRoot = visualRoot != null ? visualRoot : transform;
        return facingRoot.lossyScale.x < 0f ? -1f : 1f;
    }

    private float NormalizeDuration(float duration)
    {
        return Mathf.Max(minimumEffectDuration, duration);
    }
}
