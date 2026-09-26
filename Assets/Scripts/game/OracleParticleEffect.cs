using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum OracleParticleEffectKind
{
    BoltFlight = 0,
    BoltImpact = 1,
    Fade = 2,
    RiftBurst = 3,
    Shift = 4,
    WardCast = 5,
    WardBlock = 6,
    SightActivation = 7
}

[DisallowMultipleComponent]
public class OracleParticleEffect : MonoBehaviour
{
    private const string EffectSortingLayerName = "Effect";
    private const string LegacyEffectSortingLayerName = "effect";
    private const int ParticleSortingOrder = -1;

    [Header("Effect Identity")]
    [SerializeField, Tooltip("Selects the Oracle particle layer recipe used by this prefab.")]
    private OracleParticleEffectKind effectKind;

    [Header("Tile Relative Tuning")]
    [SerializeField, Range(0.1f, 2f), Tooltip("Multiplies generated particle shape, velocity, and size without changing the prefab root scale.")]
    private float effectScaleInTiles = 1f;

    [SerializeField, Range(0.25f, 2f), Tooltip("Multiplier for generated particle lifetimes.")]
    private float particleLifetimeMultiplier = 1f;

    [SerializeField, Range(0.25f, 2f), Tooltip("Multiplier for generated particle speeds and velocity-over-lifetime values.")]
    private float particleSpeedMultiplier = 1f;

    [SerializeField, Range(0.25f, 2f), Tooltip("Multiplier for generated particle sizes.")]
    private float particleSizeMultiplier = 1f;

    [SerializeField, Range(0.25f, 2f), Tooltip("Multiplier for generated emission rates and bursts.")]
    private float emissionMultiplier = 1f;

    [SerializeField, Range(16, 180), Tooltip("Upper bound for generated particles across this effect.")]
    private int maxParticleBudget = 120;

    [Header("Playback")]
    [SerializeField, Tooltip("Stops serialized child ParticleSystems before generated layers play. This prevents stale prefab particles and trails.")]
    private bool clearExistingParticlesOnPlay = true;

    [SerializeField, Tooltip("Use only for isolated prefab preview. Battle playback calls PlayConfigured after scale and sorting are known.")]
    private bool autoPlayOnEnable;

    private readonly List<ParticleSystem> generatedParticles = new List<ParticleSystem>();
    private Coroutine cleanupCoroutine;
    private Material additiveMaterial;
    private int sortingLayerId;
    private int baseSortingOrder;
    private float lastRequestedDuration = 0.5f;

    public OracleParticleEffectKind EffectKind => effectKind;

    private void OnEnable()
    {
        if (autoPlayOnEnable)
            PlayConfigured(0.6f, Vector3.right, null, 0, 0);
    }

    public void PlayConfigured(float duration, Vector3 direction, Material particleMaterial, int targetSortingLayerId, int targetSortingOrder)
    {
        lastRequestedDuration = Mathf.Max(0.05f, duration);
        additiveMaterial = particleMaterial;
        sortingLayerId = targetSortingLayerId != 0 ? targetSortingLayerId : GetEffectSortingLayerId();
        baseSortingOrder = targetSortingOrder;

        bool followProjectileRotation = effectKind == OracleParticleEffectKind.BoltFlight && GetComponent<BoltProjectile>() != null;
        if (!followProjectileRotation)
            ResetTransformState();

        StopExistingParticles();
        ClearGeneratedLayers();

        Vector3 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;

        switch (effectKind)
        {
            case OracleParticleEffectKind.BoltFlight:
                BuildBoltFlight(followProjectileRotation ? Vector3.right : safeDirection);
                break;
            case OracleParticleEffectKind.BoltImpact:
                BuildBoltImpact();
                break;
            case OracleParticleEffectKind.Fade:
                BuildFade();
                break;
            case OracleParticleEffectKind.RiftBurst:
                BuildRiftBurst();
                break;
            case OracleParticleEffectKind.Shift:
                BuildShift(safeDirection);
                break;
            case OracleParticleEffectKind.WardCast:
                BuildWardCast();
                break;
            case OracleParticleEffectKind.WardBlock:
                BuildWardBlock(safeDirection);
                break;
            case OracleParticleEffectKind.SightActivation:
                BuildSightActivation();
                break;
        }

        for (int i = 0; i < generatedParticles.Count; i++)
        {
            ParticleSystem particleSystem = generatedParticles[i];
            if (particleSystem == null)
                continue;

            particleSystem.Clear(true);
            particleSystem.Play(true);
        }

        if (cleanupCoroutine != null)
            StopCoroutine(cleanupCoroutine);

        cleanupCoroutine = StartCoroutine(StopAfterLifetime());
    }

    public float GetSuggestedCleanupSeconds(float duration)
    {
        return Mathf.Max(0.1f, duration) + 1.1f;
    }

    private void BuildBoltFlight(Vector3 direction)
    {
        float duration = Mathf.Max(0.4f, lastRequestedDuration);
        ParticleSystem core = CreateLayer("CoreParticles", duration, true, 0.06f, 0.12f, 0.4f, 1f, 0.06f, 0.14f, 42, 0, 54);
        ConfigureCone(core, 0.025f, 5f);
        ConfigureGradient(core, new Color(0.92f, 1f, 1f, 0.96f), new Color(0.44f, 0.24f, 1f, 0f));
        ConfigureSizeOverLife(core, 1f, 1.18f, 0f);
        ConfigureNoise(core, 0.05f, 1.1f);
        RotateToDirection(core.transform, direction);

        ParticleSystem trail = CreateLayer("TrailParticles", duration, true, 0.04f, 0.1f, 0.08f, 0.28f, 0.018f, 0.045f, 24, 0, 36);
        ConfigureCone(trail, 0.014f, 3f);
        ConfigureGradient(trail, new Color(0.48f, 0.92f, 1f, 0.76f), new Color(0.55f, 0.24f, 1f, 0f));
        ConfigureVelocity(trail, -0.28f, -0.06f, -0.035f, 0.035f);
        ConfigureNoise(trail, 0.035f, 0.9f);
        RotateToDirection(trail.transform, direction);
    }

    private void BuildBoltImpact()
    {
        ParticleSystem burst = CreateLayer("ImpactBurst", 0.34f, false, 0.15f, 0.3f, 1.5f, 3f, 0.1f, 0.25f, 0, 28, 40);
        ConfigureSphere(burst, 0.1f);
        ConfigureGradient(burst, new Color(1f, 1f, 1f, 0.95f), new Color(0.42f, 0.2f, 1f, 0f));
        ConfigureSizeOverLife(burst, 0.8f, 1.08f, 0f);

        ParticleSystem flash = CreateLayer("CenterFlash", 0.15f, false, 0.08f, 0.15f, 0f, 0f, 0.25f, 0.4f, 0, 2, 4);
        ConfigureSphere(flash, 0.02f);
        ConfigureGradient(flash, new Color(0.92f, 1f, 1f, 0.95f), new Color(0.45f, 0.9f, 1f, 0f));
        ConfigureSizeOverLife(flash, 0.65f, 1.35f, 0f);

        ParticleSystem sparks = CreateLayer("SparkBurst", 0.28f, false, 0.12f, 0.25f, 2f, 3.2f, 0.035f, 0.08f, 0, 18, 28);
        ConfigureCircle(sparks, 0.08f);
        ConfigureGradient(sparks, new Color(0.7f, 0.95f, 1f, 0.85f), new Color(0.65f, 0.35f, 1f, 0f));
    }

    private void BuildFade()
    {
        ParticleSystem dust = CreateLayer("DustParticles", 0.68f, false, 0.4f, 0.8f, 0.1f, 0.6f, 0.08f, 0.22f, 32, 0, 42);
        ConfigureBox(dust, new Vector3(0.82f, 0.95f, 0.08f));
        ConfigureGradient(dust, new Color(0.58f, 0.9f, 1f, 0.55f), new Color(0.62f, 0.34f, 1f, 0f));
        ConfigureVelocity(dust, -0.2f, 0.2f, 0.1f, 0.4f);
        ConfigureNoise(dust, 0.18f, 0.55f);

        ParticleSystem streaks = CreateLayer("StreakParticles", 0.52f, false, 0.18f, 0.36f, 0.2f, 0.65f, 0.04f, 0.11f, 16, 0, 24);
        ConfigureBox(streaks, new Vector3(0.8f, 0.72f, 0.06f));
        ConfigureGradient(streaks, new Color(0.6f, 0.75f, 1f, 0.42f), new Color(0.55f, 0.22f, 1f, 0f));
        ConfigureVelocity(streaks, -0.28f, 0.28f, 0.06f, 0.22f);
        ConfigureNoise(streaks, 0.12f, 0.7f);
    }

    private void BuildRiftBurst()
    {
        ParticleSystem ring = CreateLayer("GroundRing", 0.32f, false, 0.2f, 0.4f, 0.2f, 0.8f, 0.12f, 0.18f, 0, 1, 4);
        ConfigureCircle(ring, 0.22f);
        ConfigureGradient(ring, new Color(0.7f, 0.36f, 1f, 0.8f), new Color(0.32f, 0.08f, 0.72f, 0f));
        ConfigureSizeOverLife(ring, 0.25f, 1f, 0f);

        ParticleSystem burst = CreateLayer("UpwardBurst", 0.58f, false, 0.25f, 0.45f, 2f, 3.5f, 0.08f, 0.2f, 0, 32, 48);
        ConfigureCircle(burst, 0.24f);
        ConfigureGradient(burst, new Color(0.8f, 0.94f, 1f, 0.85f), new Color(0.42f, 0.18f, 1f, 0f));
        ConfigureVelocity(burst, -0.22f, 0.22f, 1.5f, 2.6f);
        ConfigureNoise(burst, 0.12f, 0.8f);

        ParticleSystem shards = CreateLayer("ArcaneShards", 0.5f, false, 0.24f, 0.42f, 1f, 2.2f, 0.035f, 0.08f, 0, 18, 28);
        ConfigureCircle(shards, 0.18f);
        ConfigureGradient(shards, new Color(0.55f, 0.95f, 1f, 0.8f), new Color(0.62f, 0.28f, 1f, 0f));
        ConfigureVelocity(shards, -0.28f, 0.28f, 0.8f, 1.8f);
    }

    private void BuildShift(Vector3 direction)
    {
        ParticleSystem main = CreateLayer("MainTeleportParticles", 0.46f, false, 0.2f, 0.5f, 0.5f, 2f, 0.08f, 0.18f, 0, 24, 36);
        ConfigureCircle(main, 0.18f);
        ConfigureGradient(main, new Color(0.62f, 0.94f, 1f, 0.78f), new Color(0.48f, 0.24f, 1f, 0f));
        ConfigureVelocity(main, -0.18f, 0.18f, 0.45f, 1.25f);
        ConfigureNoise(main, 0.1f, 0.7f);

        ParticleSystem vertical = CreateLayer("VerticalStreaks", 0.38f, false, 0.15f, 0.34f, 0.7f, 1.8f, 0.04f, 0.1f, 0, 12, 18);
        ConfigureCircle(vertical, 0.12f);
        ConfigureGradient(vertical, new Color(0.82f, 1f, 1f, 0.68f), new Color(0.45f, 0.28f, 1f, 0f));
        ConfigureVelocity(vertical, -0.08f, 0.08f, 0.9f, 1.8f);
        RotateToDirection(vertical.transform, direction);

        ParticleSystem pulse = CreateLayer("GroundPulse", 0.3f, false, 0.18f, 0.3f, 0.1f, 0.45f, 0.14f, 0.26f, 0, 1, 4);
        ConfigureCircle(pulse, 0.18f);
        ConfigureGradient(pulse, new Color(0.42f, 0.72f, 1f, 0.58f), new Color(0.46f, 0.22f, 1f, 0f));
        ConfigureSizeOverLife(pulse, 0.3f, 0.92f, 0f);
    }

    private void BuildWardCast()
    {
        ParticleSystem aura = CreateLayer("AuraParticles", 0.76f, false, 0.4f, 0.7f, 0.2f, 1.1f, 0.06f, 0.14f, 34, 12, 46);
        ConfigureCircle(aura, 0.42f);
        ConfigureGradient(aura, new Color(0.72f, 0.98f, 1f, 0.66f), new Color(0.28f, 0.68f, 1f, 0f));
        ConfigureVelocity(aura, -0.12f, 0.12f, 0.1f, 0.42f);
        ConfigureNoise(aura, 0.08f, 0.7f);

        ParticleSystem dust = CreateLayer("ShieldDust", 0.72f, false, 0.28f, 0.62f, 0.1f, 0.55f, 0.05f, 0.11f, 20, 10, 30);
        ConfigureCircle(dust, 0.48f);
        ConfigureGradient(dust, new Color(0.42f, 0.92f, 1f, 0.5f), new Color(0.62f, 0.35f, 1f, 0f));
        ConfigureVelocity(dust, -0.08f, 0.08f, 0.1f, 0.32f);

        ParticleSystem pulse = CreateLayer("ActivationPulse", 0.24f, false, 0.16f, 0.28f, 0.1f, 0.4f, 0.18f, 0.32f, 0, 1, 3);
        ConfigureCircle(pulse, 0.36f);
        ConfigureGradient(pulse, new Color(0.85f, 1f, 1f, 0.72f), new Color(0.3f, 0.78f, 1f, 0f));
        ConfigureSizeOverLife(pulse, 0.25f, 0.95f, 0f);
    }

    private void BuildWardBlock(Vector3 contactDirection)
    {
        ParticleSystem sparks = CreateLayer("ImpactSparks", 0.34f, false, 0.1f, 0.25f, 2f, 3.4f, 0.08f, 0.2f, 0, 24, 36);
        ConfigureCone(sparks, 0.08f, 28f);
        ConfigureGradient(sparks, new Color(1f, 0.92f, 0.55f, 0.9f), new Color(0.42f, 0.92f, 1f, 0f));
        ConfigureSizeOverLife(sparks, 1f, 0.8f, 0f);
        RotateToDirection(sparks.transform, contactDirection);

        ParticleSystem ring = CreateLayer("ShockRing", 0.22f, false, 0.15f, 0.25f, 0.1f, 0.35f, 0.2f, 0.3f, 0, 1, 3);
        ConfigureCircle(ring, 0.22f);
        ConfigureGradient(ring, new Color(0.8f, 1f, 1f, 0.78f), new Color(0.5f, 0.9f, 1f, 0f));
        ConfigureSizeOverLife(ring, 0.24f, 0.92f, 0f);

        ParticleSystem flash = CreateLayer("ContactFlash", 0.14f, false, 0.08f, 0.15f, 0f, 0f, 0.22f, 0.36f, 0, 2, 4);
        ConfigureSphere(flash, 0.02f);
        ConfigureGradient(flash, new Color(0.9f, 1f, 1f, 0.92f), new Color(0.44f, 0.86f, 1f, 0f));
    }

    private void BuildSightActivation()
    {
        ParticleSystem awakening = CreateLayer("AwakeningParticles", 0.9f, false, 0.5f, 0.9f, 0.1f, 0.8f, 0.05f, 0.16f, 28, 16, 44);
        ConfigureCircle(awakening, 0.35f);
        ConfigureGradient(awakening, new Color(0.88f, 0.94f, 1f, 0.72f), new Color(0.56f, 0.32f, 1f, 0f));
        ConfigureVelocity(awakening, -0.16f, 0.16f, 0.15f, 0.62f);
        ConfigureNoise(awakening, 0.1f, 0.6f);

        ParticleSystem flash = CreateLayer("EyeFlash", 0.22f, false, 0.1f, 0.25f, 0f, 0.2f, 0.3f, 0.6f, 0, 2, 4);
        ConfigureSphere(flash, 0.03f);
        ConfigureGradient(flash, new Color(1f, 1f, 1f, 0.92f), new Color(0.62f, 0.52f, 1f, 0f));
        ConfigureSizeOverLife(flash, 0.55f, 1.15f, 0f);

        ParticleSystem rays = CreateLayer("LightRays", 0.36f, false, 0.16f, 0.34f, 0.3f, 0.9f, 0.04f, 0.1f, 0, 12, 18);
        ConfigureCircle(rays, 0.2f);
        ConfigureGradient(rays, new Color(0.9f, 1f, 1f, 0.58f), new Color(0.6f, 0.36f, 1f, 0f));
        ConfigureVelocity(rays, -0.25f, 0.25f, 0.2f, 0.8f);

        ParticleSystem runes = CreateLayer("RuneParticles", 0.8f, false, 0.45f, 0.8f, 0.1f, 0.55f, 0.035f, 0.08f, 12, 10, 24);
        ConfigureCircle(runes, 0.38f);
        ConfigureGradient(runes, new Color(1f, 0.78f, 0.35f, 0.62f), new Color(0.55f, 0.25f, 1f, 0f));
        ConfigureVelocity(runes, -0.12f, 0.12f, 0.12f, 0.42f);
    }

    private ParticleSystem CreateLayer(
        string layerName,
        float duration,
        bool loop,
        float lifetimeMin,
        float lifetimeMax,
        float speedMin,
        float speedMax,
        float sizeMin,
        float sizeMax,
        int rateOverTime,
        int burstCount,
        int maxParticles)
    {
        GameObject layer = new GameObject(layerName, typeof(ParticleSystem));
        layer.transform.SetParent(transform, false);
        layer.transform.localPosition = Vector3.zero;
        layer.transform.localRotation = Quaternion.identity;
        layer.transform.localScale = Vector3.one;

        ParticleSystem ps = layer.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Clear(true);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.duration = Mathf.Max(0.05f, duration);
        main.loop = loop;
        main.startLifetime = new ParticleSystem.MinMaxCurve(ScaleLifetime(lifetimeMin), ScaleLifetime(lifetimeMax));
        main.startSpeed = new ParticleSystem.MinMaxCurve(ScaleVelocity(speedMin), ScaleVelocity(speedMax));
        main.startSize = new ParticleSystem.MinMaxCurve(ScaleSize(sizeMin), ScaleSize(sizeMax));
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Min(maxParticleBudget, Mathf.Max(1, maxParticles));

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = Mathf.Max(0f, rateOverTime * emissionMultiplier);
        emission.rateOverDistance = 0f;
        if (burstCount > 0)
        {
            short burst = (short)Mathf.Clamp(Mathf.RoundToInt(burstCount * emissionMultiplier), 1, main.maxParticles);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burst) });
        }
        else
        {
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());
        }

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = ParticleSortingOrder;
        renderer.maxParticleSize = 0.5f;
        if (additiveMaterial != null)
            renderer.sharedMaterial = additiveMaterial;

        generatedParticles.Add(ps);
        return ps;
    }

    private static int GetEffectSortingLayerId()
    {
        int layerId = SortingLayer.NameToID(EffectSortingLayerName);
        return layerId != 0 ? layerId : SortingLayer.NameToID(LegacyEffectSortingLayerName);
    }

    private void ConfigureCone(ParticleSystem ps, float radius, float angle)
    {
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = ScaleDistance(radius);
        shape.angle = Mathf.Clamp(angle, 0f, 90f);
    }

    private void ConfigureCircle(ParticleSystem ps, float radius)
    {
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = ScaleDistance(radius);
    }

    private void ConfigureSphere(ParticleSystem ps, float radius)
    {
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = ScaleDistance(radius);
    }

    private void ConfigureBox(ParticleSystem ps, Vector3 localScale)
    {
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = localScale * Mathf.Max(0.01f, effectScaleInTiles);
    }

    private void ConfigureGradient(ParticleSystem ps, Color start, Color end)
    {
        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(start.r, start.g, start.b), 0f),
                new GradientColorKey(new Color(end.r, end.g, end.b), 1f)
            },
            new[]
            {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(end.a, 1f)
            });
        color.color = gradient;
    }

    private void ConfigureSizeOverLife(ParticleSystem ps, float start, float middle, float end)
    {
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;

        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, start),
            new Keyframe(0.45f, middle),
            new Keyframe(1f, end));
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private void ConfigureVelocity(ParticleSystem ps, float xMin, float xMax, float yMin, float yMax)
    {
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(ScaleVelocity(xMin), ScaleVelocity(xMax));
        velocity.y = new ParticleSystem.MinMaxCurve(ScaleVelocity(yMin), ScaleVelocity(yMax));
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    private void ConfigureNoise(ParticleSystem ps, float strength, float frequency)
    {
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = ScaleDistance(strength);
        noise.frequency = Mathf.Max(0.01f, frequency);
        noise.scrollSpeed = 0.08f;
    }

    private void RotateToDirection(Transform target, Vector3 direction)
    {
        if (target == null || direction.sqrMagnitude < 0.0001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        target.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void ResetTransformState()
    {
        transform.localRotation = Quaternion.identity;
    }

    private void StopExistingParticles()
    {
        if (!clearExistingParticlesOnPlay)
            return;

        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null)
                continue;

            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles[i].Clear(true);
            if (!generatedParticles.Contains(particles[i]))
                particles[i].gameObject.SetActive(false);
        }
    }

    private void ClearGeneratedLayers()
    {
        generatedParticles.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || child.GetComponent<ParticleSystem>() == null)
                continue;

            Destroy(child.gameObject);
        }
    }

    private IEnumerator StopAfterLifetime()
    {
        yield return new WaitForSecondsRealtime(GetSuggestedCleanupSeconds(lastRequestedDuration));

        for (int i = 0; i < generatedParticles.Count; i++)
        {
            if (generatedParticles[i] != null)
                generatedParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        cleanupCoroutine = null;
    }

    private float ScaleDistance(float value)
    {
        return value * Mathf.Max(0.01f, effectScaleInTiles);
    }

    private float ScaleVelocity(float value)
    {
        return ScaleDistance(value) * particleSpeedMultiplier;
    }

    private float ScaleSize(float value)
    {
        return ScaleDistance(value) * particleSizeMultiplier;
    }

    private float ScaleLifetime(float value)
    {
        return Mathf.Max(0.01f, value * particleLifetimeMultiplier);
    }
}
