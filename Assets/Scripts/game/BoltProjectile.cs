using System;
using System.Collections;
using UnityEngine;

public enum BoltProjectileState
{
    Launch,
    Armed,
    Travel,
    Reflected,
    Finished
}

public enum BoltProjectileFinishReason
{
    None,
    Hit,
    Expired
}

public enum BoltDefenderState
{
    Normal,
    Defense,
    Parry,
    Jump
}

[DisallowMultipleComponent]
public class BoltProjectile : MonoBehaviour
{
    [Serializable]
    public struct RuntimeConfig
    {
        public CharacterUnit CasterUnit;
        public CharacterUnit TargetUnit;
        public Vector3 SpawnPosition;
        public Vector3 ForwardDirection;
        public float TileWorldSize;
        public int Damage;
        public int MinHitRange;
        public int MaxHitRange;
        public int GridDistanceOverride;
        public float TotalStepDurationSeconds;
        public float LaunchDurationSeconds;
        public float ArmHoldDurationSeconds;
        public float LaunchArcHeightWorld;
        public float TravelSpeedWorldUnitsPerSecond;
        public float BoltStartHeightWorld;
        public float BoltStartForwardOffsetWorld;
        public float BoltTravelDurationSeconds;
        public float BoltEndHeightOffsetWorld;
        public AnimationCurve BoltFallCurve;
        public float BoltInverseCurveStrength;
        public float BoltAccelerationExponent;
        public float SpriteAngleOffset;
        public BoltDefenderState DefenderState;
        public bool ApplyStandaloneDamage;
        public bool ReflectOnTargetImpact;
        public Vector3 ReflectedReturnTargetPosition;
        public float ReflectedInitialSpeedWorldUnitsPerSecond;
        public float ReflectedAccelerationWorldUnitsPerSecondSquared;
        public float ReflectedMaxSpeedWorldUnitsPerSecond;
        public GameObject ArmEffectPrefabOverride;
        public GameObject HitEffectPrefabOverride;
    }

    [Header("Visual References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private Collider2D hitbox;
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private bool createRuntimeHitboxIfMissing = true;
    [SerializeField] private Vector2 runtimeHitboxSize = Vector2.one;

    [Header("Sprites")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite armedSprite;

    [Header("Effects")]
    [SerializeField] private GameObject armEffectPrefab;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private bool spawnHitEffectOnImpact;

    [Header("Animator Triggers")]
    [SerializeField] private string launchTrigger = "Launch";
    [SerializeField] private string armTrigger = "Arm";
    [SerializeField] private string hitTrigger = "Hit";

    [Header("Grid Rules")]
    [SerializeField] private int damage = 3;
    [SerializeField] private int minHitRange = 2;
    [SerializeField] private int maxHitRange = 4;
    [SerializeField] private float tileWorldSize = 1f;
    [SerializeField] private int casterGridIndex;
    [SerializeField] private int targetGridIndex;
    [SerializeField] private bool useExplicitGridIndexes;

    [Header("Flight Tuning")]
    [SerializeField] private float launchDuration = 0.22f;
    [SerializeField] private float armHoldDuration = 0.04f;
    [SerializeField] private float launchArcHeight = 0.35f;
    [SerializeField] private float armDistanceTiles = 1f;
    [SerializeField] private float hitLeadDistanceTiles = 0.12f;
    [SerializeField] private float travelSpeed = 8f;
    [SerializeField] private bool destroyOnFinish = true;
    [SerializeField] private float cleanupDelay = 0.05f;

    [Header("Meteor Trajectory")]
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

    [Header("Standalone Test Damage")]
    [SerializeField] private bool applyStandaloneDamage;
    [SerializeField] private BoltDefenderState standaloneDefenderState = BoltDefenderState.Normal;

    [Header("Reflected Flight")]
    [SerializeField] private float reflectedInitialSpeed = 6f;
    [SerializeField] private float reflectedAcceleration = 30f;
    [SerializeField] private float reflectedMaxSpeed = 18f;

    private CharacterUnit casterUnit;
    private CharacterUnit targetUnit;
    private Vector3 launchStartPosition;
    private Vector3 armPosition;
    private Vector3 rangeOriginPosition;
    private Vector3 travelEndPosition;
    private Vector3 forwardDirection = Vector3.right;
    private float launchTimer;
    private float armTimer;
    private float travelTimer;
    private float runtimeTravelDuration;
    private float armPathT;
    private bool initialized;
    private bool hasHit;
    private int gridDistanceOverride = -1;
    private GameObject runtimeArmEffectPrefab;
    private GameObject runtimeHitEffectPrefab;
    private BoltDefenderState runtimeDefenderState;
    private bool reflectOnTargetImpact;
    private Vector3 reflectedReturnTargetPosition;
    private Vector3 reflectedDirection;
    private float reflectedRuntimeSpeed;
    private Coroutine cleanupCoroutine;

    public event Action<BoltProjectile> Armed;
    public event Action<BoltProjectile, CharacterUnit> Impacted;
    public event Action<BoltProjectile, Vector3> Reflected;
    public event Action<BoltProjectile, Vector3> ReflectedImpact;
    public event Action<BoltProjectile, BoltProjectileFinishReason> Finished;

    public BoltProjectileState State { get; private set; } = BoltProjectileState.Finished;
    public BoltProjectileFinishReason FinishReason { get; private set; } = BoltProjectileFinishReason.None;
    public Vector3 ForwardDirection => forwardDirection;
    public bool IsArmed => State == BoltProjectileState.Armed || State == BoltProjectileState.Travel;

    private void Awake()
    {
        ResolveReferences();
        EnsureRuntimeHitbox();
        SetHitboxEnabled(false);
        SetTrailEnabled(false);
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (!initialized)
            return;

        switch (State)
        {
            case BoltProjectileState.Launch:
                UpdateLaunchPhase();
                break;
            case BoltProjectileState.Armed:
                UpdateArmPhase();
                break;
            case BoltProjectileState.Travel:
                UpdateTravelPhase();
                break;
            case BoltProjectileState.Reflected:
                UpdateReflectedPhase();
                break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsArmed || hasHit || other == null)
            return;

        CharacterUnit hitUnit = other.GetComponentInParent<CharacterUnit>();
        if (hitUnit == null || hitUnit == casterUnit)
            return;

        if (targetUnit != null && hitUnit != targetUnit)
            return;

        if (!IsUnitInLegalHitRange(hitUnit))
            return;

        if (reflectOnTargetImpact)
        {
            BeginReflectedReturn(hitUnit);
            return;
        }

        FinishWithHit(hitUnit);
    }

    public void InitializePresentation(RuntimeConfig config)
    {
        ResolveReferences();
        EnsureRuntimeHitbox();

        casterUnit = config.CasterUnit;
        targetUnit = config.TargetUnit;
        damage = Mathf.Max(0, config.Damage > 0 ? config.Damage : damage);
        minHitRange = Mathf.Max(0, config.MinHitRange > 0 ? config.MinHitRange : minHitRange);
        maxHitRange = Mathf.Max(minHitRange, config.MaxHitRange > 0 ? config.MaxHitRange : maxHitRange);
        tileWorldSize = Mathf.Max(0.01f, config.TileWorldSize > 0f ? config.TileWorldSize : tileWorldSize);
        launchDuration = Mathf.Max(0.01f, config.LaunchDurationSeconds > 0f ? config.LaunchDurationSeconds : launchDuration);
        armHoldDuration = Mathf.Max(0f, config.ArmHoldDurationSeconds >= 0f ? config.ArmHoldDurationSeconds : armHoldDuration);
        launchArcHeight = Mathf.Max(0f, config.LaunchArcHeightWorld >= 0f ? config.LaunchArcHeightWorld : launchArcHeight);
        gridDistanceOverride = config.GridDistanceOverride > 0 ? config.GridDistanceOverride : -1;
        runtimeDefenderState = config.DefenderState;
        applyStandaloneDamage = config.ApplyStandaloneDamage || applyStandaloneDamage;
        runtimeArmEffectPrefab = config.ArmEffectPrefabOverride != null ? config.ArmEffectPrefabOverride : armEffectPrefab;
        runtimeHitEffectPrefab = config.HitEffectPrefabOverride != null ? config.HitEffectPrefabOverride : hitEffectPrefab;
        reflectOnTargetImpact = config.ReflectOnTargetImpact;
        reflectedReturnTargetPosition = config.ReflectedReturnTargetPosition;

        if (config.ReflectedInitialSpeedWorldUnitsPerSecond > 0f)
            reflectedInitialSpeed = config.ReflectedInitialSpeedWorldUnitsPerSecond;

        if (config.ReflectedAccelerationWorldUnitsPerSecondSquared > 0f)
            reflectedAcceleration = config.ReflectedAccelerationWorldUnitsPerSecondSquared;

        if (config.ReflectedMaxSpeedWorldUnitsPerSecond > 0f)
            reflectedMaxSpeed = config.ReflectedMaxSpeedWorldUnitsPerSecond;

        ApplyRuntimeTrajectoryConfig(config);

        rangeOriginPosition = config.SpawnPosition;
        Vector3 casterCenter = casterUnit != null ? GetUnitCenter(casterUnit) : rangeOriginPosition;
        Vector3 targetCenter = targetUnit != null
            ? GetUnitCenter(targetUnit)
            : rangeOriginPosition + NormalizeHorizontalDirection(config.ForwardDirection) * GetMaxTravelDistanceWorld();

        forwardDirection = ResolveDirectionFromCurrentPositions(casterCenter, targetCenter, config.ForwardDirection);
        launchStartPosition = ResolveMeteorStartPosition(casterCenter);
        travelEndPosition = ResolveMeteorEndPosition(targetCenter);
        armPathT = ResolveArmPathT();
        armPosition = EvaluateMeteorPath(armPathT);

        float configuredTravelSpeed = config.TravelSpeedWorldUnitsPerSecond;
        if (configuredTravelSpeed > 0f)
        {
            travelSpeed = configuredTravelSpeed;
        }

        runtimeTravelDuration = ResolveTravelDuration(config, configuredTravelSpeed);

        transform.position = launchStartPosition;
        FaceMeteorPathDirection();
        SetHitboxEnabled(false);
        SetTrailEnabled(false);
        ApplySprite(idleSprite);
        TriggerAnimator(launchTrigger);

        launchTimer = 0f;
        armTimer = 0f;
        travelTimer = 0f;
        hasHit = false;
        initialized = true;
        FinishReason = BoltProjectileFinishReason.None;
        State = BoltProjectileState.Launch;
    }

    public void InitializeGridIndexes(int casterIndex, int targetIndex)
    {
        casterGridIndex = casterIndex;
        targetGridIndex = targetIndex;
        useExplicitGridIndexes = true;
    }

    public void ForceExpire()
    {
        if (State == BoltProjectileState.Finished)
            return;

        FinishExpired();
    }

    private void UpdateLaunchPhase()
    {
        launchTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(launchTimer / Mathf.Max(0.01f, launchDuration));
        transform.position = EvaluateLaunchPosition(t);
        FaceMeteorPathDirection();

        if (t >= 1f)
            ArmProjectile();
    }

    private Vector3 EvaluateLaunchPosition(float t)
    {
        float motionT = EvaluateInverseCurveProgress(t);
        return EvaluateMeteorPath(Mathf.Lerp(0f, armPathT, motionT));
    }

    private void ArmProjectile()
    {
        State = BoltProjectileState.Armed;
        transform.position = armPosition;
        FaceMeteorPathDirection();
        SetHitboxEnabled(true);
        SetTrailEnabled(true);
        ApplySprite(armedSprite);
        TriggerAnimator(armTrigger);
        SpawnRuntimeEffect(runtimeArmEffectPrefab, transform.position);
        Armed?.Invoke(this);
    }

    private void UpdateArmPhase()
    {
        armTimer += Time.unscaledDeltaTime;
        if (armTimer >= armHoldDuration)
        {
            travelTimer = 0f;
            State = BoltProjectileState.Travel;
        }
    }

    private void UpdateTravelPhase()
    {
        travelTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(travelTimer / Mathf.Max(0.01f, runtimeTravelDuration));
        float motionT = EvaluateInverseCurveProgress(t);
        transform.position = EvaluateMeteorPath(Mathf.Lerp(armPathT, 1f, motionT));
        FaceMeteorPathDirection();

        bool canResolveTargetImpact = !hasHit && targetUnit != null && IsUnitInLegalHitRange(targetUnit);
        if (canResolveTargetImpact && HasReachedTargetLine(targetUnit))
        {
            if (reflectOnTargetImpact)
                BeginReflectedReturn(targetUnit);
            else
                FinishWithHit(targetUnit);

            return;
        }

        if (GetProjectedDistance(transform.position) >= GetMaxTravelDistanceWorld())
        {
            FinishExpired();
            return;
        }

        if (t >= 1f)
        {
            if (canResolveTargetImpact)
            {
                if (reflectOnTargetImpact)
                    BeginReflectedReturn(targetUnit);
                else
                    FinishWithHit(targetUnit);
            }
            else
            {
                FinishExpired();
            }
        }
    }

    private bool HasReachedTargetLine(CharacterUnit unit)
    {
        if (unit == null)
            return false;

        float currentDistance = GetProjectedDistance(transform.position);
        float targetDistance = GetProjectedDistance(GetUnitCenter(unit));
        float hitLead = Mathf.Max(0f, hitLeadDistanceTiles * tileWorldSize);
        return targetDistance >= 0f && currentDistance >= targetDistance - hitLead;
    }

    private bool IsUnitInLegalHitRange(CharacterUnit unit)
    {
        int distance = GetGridDistance(unit);
        return BoltGridRangeHelper.IsDistanceInRange(distance, minHitRange, maxHitRange);
    }

    private int GetGridDistance(CharacterUnit unit)
    {
        if (gridDistanceOverride > 0)
            return gridDistanceOverride;

        if (useExplicitGridIndexes)
            return BoltGridRangeHelper.GetAbsoluteGridDistance(casterGridIndex, targetGridIndex);

        float projectedWorldDistance = GetProjectedDistance(GetUnitCenter(unit));
        return Mathf.Max(0, Mathf.RoundToInt(projectedWorldDistance / tileWorldSize));
    }

    private float GetProjectedDistance(Vector3 worldPosition)
    {
        return Vector3.Dot(worldPosition - rangeOriginPosition, forwardDirection);
    }

    private float GetMaxTravelDistanceWorld()
    {
        return Mathf.Max(tileWorldSize, maxHitRange * tileWorldSize);
    }

    private Vector3 GetUnitCenter(CharacterUnit unit)
    {
        if (unit == null)
            return transform.position;

        Collider2D collider2D = unit.GetComponentInChildren<Collider2D>();
        if (collider2D != null)
            return collider2D.bounds.center;

        Renderer renderer = unit.GetComponentInChildren<Renderer>();
        if (renderer != null)
            return renderer.bounds.center;

        return unit.transform.position;
    }

    private void ApplyRuntimeTrajectoryConfig(RuntimeConfig config)
    {
        boltStartHeight = Mathf.Max(0f, config.BoltStartHeightWorld);
        boltStartForwardOffset = Mathf.Max(0f, config.BoltStartForwardOffsetWorld);
        boltEndHeightOffset = config.BoltEndHeightOffsetWorld;
        spriteAngleOffset = config.SpriteAngleOffset;

        if (config.BoltTravelDurationSeconds > 0f)
            boltTravelDuration = config.BoltTravelDurationSeconds;

        if (config.BoltFallCurve != null && config.BoltFallCurve.length > 0)
            boltFallCurve = config.BoltFallCurve;

        if (config.BoltInverseCurveStrength > 0f)
            boltInverseCurveStrength = Mathf.Max(1.01f, config.BoltInverseCurveStrength);

        if (config.BoltAccelerationExponent > 0f)
            boltAccelerationExponent = Mathf.Max(0.1f, config.BoltAccelerationExponent);
    }

    private Vector3 ResolveDirectionFromCurrentPositions(Vector3 casterCenter, Vector3 targetCenter, Vector3 fallbackDirection)
    {
        float deltaX = targetCenter.x - casterCenter.x;
        if (Mathf.Abs(deltaX) >= 0.001f)
            return deltaX < 0f ? Vector3.left : Vector3.right;

        return NormalizeHorizontalDirection(fallbackDirection);
    }

    private Vector3 ResolveMeteorStartPosition(Vector3 casterCenter)
    {
        return casterCenter
               - forwardDirection * boltStartForwardOffset
               + Vector3.up * boltStartHeight;
    }

    private Vector3 ResolveMeteorEndPosition(Vector3 targetCenter)
    {
        Vector3 endPosition;
        if (targetUnit != null && IsUnitInLegalHitRange(targetUnit))
        {
            endPosition = targetCenter;
        }
        else
        {
            endPosition = rangeOriginPosition + forwardDirection * GetMaxTravelDistanceWorld();
            endPosition.y = targetUnit != null ? targetCenter.y : rangeOriginPosition.y;
        }

        endPosition.y += boltEndHeightOffset;
        endPosition.z = launchStartPosition.z;
        return endPosition;
    }

    private float ResolveArmPathT()
    {
        float pathDistance = GetMeteorPathDistance();
        if (pathDistance <= 0.001f)
            return 1f;

        float armDistance = Mathf.Max(0.01f, armDistanceTiles * tileWorldSize);
        return Mathf.Clamp01(armDistance / pathDistance);
    }

    private float ResolveTravelDuration(RuntimeConfig config, float configuredTravelSpeed)
    {
        if (config.BoltTravelDurationSeconds > 0f)
            return Mathf.Max(0.01f, config.BoltTravelDurationSeconds);

        float pathDistance = Mathf.Max(0.01f, GetMeteorPathDistance() * Mathf.Max(0f, 1f - armPathT));
        if (configuredTravelSpeed > 0f)
            return Mathf.Max(0.01f, pathDistance / configuredTravelSpeed);

        if (config.TotalStepDurationSeconds > 0f)
            return Mathf.Max(0.05f, config.TotalStepDurationSeconds - launchDuration - armHoldDuration);

        return Mathf.Max(0.01f, boltTravelDuration);
    }

    private float GetMeteorPathDistance()
    {
        return Mathf.Abs(Vector3.Dot(travelEndPosition - launchStartPosition, forwardDirection));
    }

    private Vector3 EvaluateMeteorPath(float t)
    {
        t = Mathf.Clamp01(t);
        float fallT = EvaluateFallCurve(t);

        float x = Mathf.Lerp(launchStartPosition.x, travelEndPosition.x, t);
        float y = Mathf.Lerp(launchStartPosition.y, travelEndPosition.y, fallT);
        float z = Mathf.Lerp(launchStartPosition.z, travelEndPosition.z, t);
        return new Vector3(x, y, z);
    }

    private float EvaluateFallCurve(float t)
    {
        if (boltFallCurve == null || boltFallCurve.length == 0)
            return t;

        return Mathf.Clamp01(boltFallCurve.Evaluate(Mathf.Clamp01(t)));
    }

    private float EvaluateInverseCurveProgress(float t)
    {
        t = Mathf.Clamp01(t);
        float acceleratedT = Mathf.Pow(t, Mathf.Max(0.1f, boltAccelerationExponent));
        float strength = Mathf.Max(1.01f, boltInverseCurveStrength);
        float denominator = strength - (strength - 1f) * acceleratedT;
        return Mathf.Clamp01(acceleratedT / Mathf.Max(0.0001f, denominator));
    }

    private void FinishWithHit(CharacterUnit hitUnit)
    {
        if (State == BoltProjectileState.Finished)
            return;

        hasHit = true;
        FinishReason = BoltProjectileFinishReason.Hit;
        SetHitboxEnabled(false);
        SetTrailEnabled(false);
        ApplyStandaloneDamage(hitUnit);
        TriggerAnimator(hitTrigger);

        if (spawnHitEffectOnImpact)
            SpawnRuntimeEffect(runtimeHitEffectPrefab, GetUnitCenter(hitUnit));

        Impacted?.Invoke(this, hitUnit);
        Finish(BoltProjectileFinishReason.Hit);
    }

    private void BeginReflectedReturn(CharacterUnit parryUnit)
    {
        if (State == BoltProjectileState.Finished)
            return;

        Vector3 contactPosition = parryUnit != null ? GetUnitCenter(parryUnit) : transform.position;
        contactPosition.z = transform.position.z;
        transform.position = contactPosition;

        Vector3 returnTarget = reflectedReturnTargetPosition;
        if (returnTarget.sqrMagnitude < 0.0001f && casterUnit != null)
            returnTarget = GetUnitCenter(casterUnit);

        returnTarget.z = contactPosition.z;
        reflectedReturnTargetPosition = returnTarget;

        Vector3 toReturnTarget = reflectedReturnTargetPosition - contactPosition;
        if (toReturnTarget.sqrMagnitude < 0.0001f)
        {
            FinishReflectedImpact();
            return;
        }

        hasHit = true;
        SetHitboxEnabled(false);
        reflectedDirection = toReturnTarget.normalized;
        forwardDirection = reflectedDirection;
        reflectedRuntimeSpeed = Mathf.Max(0.01f, reflectedInitialSpeed);
        reflectedMaxSpeed = Mathf.Max(reflectedRuntimeSpeed, reflectedMaxSpeed);
        reflectedAcceleration = Mathf.Max(0f, reflectedAcceleration);

        FaceDirection(reflectedDirection);
        Reflected?.Invoke(this, contactPosition);
        State = BoltProjectileState.Reflected;
    }

    private void UpdateReflectedPhase()
    {
        float deltaTime = Time.unscaledDeltaTime;
        reflectedRuntimeSpeed = Mathf.Min(reflectedMaxSpeed, reflectedRuntimeSpeed + reflectedAcceleration * deltaTime);

        Vector3 toTarget = reflectedReturnTargetPosition - transform.position;
        float remainingDistance = toTarget.magnitude;
        if (remainingDistance <= 0.001f)
        {
            FinishReflectedImpact();
            return;
        }

        float moveDistance = reflectedRuntimeSpeed * deltaTime;
        if (moveDistance >= remainingDistance)
        {
            transform.position = reflectedReturnTargetPosition;
            FaceDirection(reflectedDirection);
            FinishReflectedImpact();
            return;
        }

        transform.position += reflectedDirection * moveDistance;
        FaceDirection(reflectedDirection);
    }

    private void FinishReflectedImpact()
    {
        if (State == BoltProjectileState.Finished)
            return;

        transform.position = reflectedReturnTargetPosition;
        FinishReason = BoltProjectileFinishReason.Hit;
        SetHitboxEnabled(false);
        SetTrailEnabled(false);
        TriggerAnimator(hitTrigger);
        ReflectedImpact?.Invoke(this, reflectedReturnTargetPosition);
        Finish(BoltProjectileFinishReason.Hit);
    }

    private void FinishExpired()
    {
        if (State == BoltProjectileState.Finished)
            return;

        SetHitboxEnabled(false);
        SetTrailEnabled(false);
        Finish(BoltProjectileFinishReason.Expired);
    }

    private void Finish(BoltProjectileFinishReason reason)
    {
        FinishReason = reason;
        State = BoltProjectileState.Finished;
        Finished?.Invoke(this, reason);

        if (destroyOnFinish)
            StartCleanup();
    }

    private void StartCleanup()
    {
        if (cleanupCoroutine != null)
            StopCoroutine(cleanupCoroutine);

        cleanupCoroutine = StartCoroutine(DestroyAfterUnscaledDelay(Mathf.Max(0f, cleanupDelay)));
    }

    private IEnumerator DestroyAfterUnscaledDelay(float delay)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (gameObject != null)
            Destroy(gameObject);
    }

    private void ApplyStandaloneDamage(CharacterUnit hitUnit)
    {
        if (!applyStandaloneDamage || hitUnit == null)
            return;

        BoltDefenderState defenderState = runtimeDefenderState != 0
            ? runtimeDefenderState
            : standaloneDefenderState;

        switch (defenderState)
        {
            case BoltDefenderState.Defense:
                return;
            case BoltDefenderState.Parry:
                if (casterUnit != null)
                    casterUnit.TakeDamage(damage);
                return;
            case BoltDefenderState.Normal:
            case BoltDefenderState.Jump:
            default:
                hitUnit.TakeDamage(damage);
                return;
        }
    }

    private void ResolveReferences()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (hitbox == null)
            hitbox = GetComponentInChildren<Collider2D>(true);

        if (trailRenderer == null)
            trailRenderer = GetComponentInChildren<TrailRenderer>(true);
    }

    private void EnsureRuntimeHitbox()
    {
        if (hitbox != null || !createRuntimeHitboxIfMissing)
            return;

        BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;
        boxCollider.size = runtimeHitboxSize;
        hitbox = boxCollider;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
    }

    private void SetHitboxEnabled(bool enabled)
    {
        if (hitbox != null)
            hitbox.enabled = enabled;
    }

    private void SetTrailEnabled(bool enabled)
    {
        if (trailRenderer == null)
            return;

        trailRenderer.enabled = enabled;
        if (enabled)
            trailRenderer.Clear();
    }

    private void ApplySprite(Sprite sprite)
    {
        if (spriteRenderer != null && sprite != null)
            spriteRenderer.sprite = sprite;
    }

    private void TriggerAnimator(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            return;

        if (!HasAnimatorTrigger(triggerName))
            return;

        ResetAnimatorTriggerIfExists(launchTrigger);
        ResetAnimatorTriggerIfExists(armTrigger);
        ResetAnimatorTriggerIfExists(hitTrigger);
        animator.SetTrigger(triggerName);
    }

    private bool HasAnimatorTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i] != null &&
                parameters[i].type == AnimatorControllerParameterType.Trigger &&
                parameters[i].name == triggerName)
            {
                return true;
            }
        }

        return false;
    }

    private void ResetAnimatorTriggerIfExists(string triggerName)
    {
        if (!HasAnimatorTrigger(triggerName))
            return;

        animator.ResetTrigger(triggerName);
    }

    private void SpawnRuntimeEffect(GameObject prefab, Vector3 position)
    {
        if (prefab == null)
            return;

        GameObject spawned = Instantiate(prefab, position, Quaternion.identity);
        Destroy(spawned, 2f);
    }

    private Vector3 NormalizeHorizontalDirection(Vector3 direction)
    {
        if (Mathf.Abs(direction.x) < 0.001f)
            return Vector3.right;

        return direction.x < 0f ? Vector3.left : Vector3.right;
    }

    private void FaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = forwardDirection;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + spriteAngleOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void FaceMeteorPathDirection()
    {
        FaceDirection(travelEndPosition - launchStartPosition);
    }
}

public static class BoltGridRangeHelper
{
    public static int GetAbsoluteGridDistance(int casterGridIndex, int targetGridIndex)
    {
        return Mathf.Abs(targetGridIndex - casterGridIndex);
    }

    public static bool IsDistanceInRange(int distance, int minRangeInclusive, int maxRangeInclusive)
    {
        return distance >= minRangeInclusive && distance <= maxRangeInclusive;
    }

    public static bool IsGridIndexInDirectedRange(
        int casterGridIndex,
        int targetGridIndex,
        int directionSign,
        int minRangeInclusive,
        int maxRangeInclusive)
    {
        int signedDistance = (targetGridIndex - casterGridIndex) * Math.Sign(directionSign == 0 ? 1 : directionSign);
        return signedDistance >= minRangeInclusive && signedDistance <= maxRangeInclusive;
    }
}
