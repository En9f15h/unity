using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightningStrikeVFXRoot : MonoBehaviour
{
    [Header("Particles Played On Hit")]
    [SerializeField] private ParticleSystem branchGlow;
    [SerializeField] private ParticleSystem impactBurst;

    [Header("Timing Sync")]
    [Tooltip("Seconds from effect spawn until lightning visually hits the sword.")]
    [SerializeField] private float impactDelay = 0.2f;

    [Tooltip("Seconds after hit before destroying the whole root.")]
    [SerializeField] private float destroyDelay = 1.0f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Auto Play")]
    [SerializeField] private bool playUnassignedChildParticles = true;
    [SerializeField] private bool clearBeforePlay = true;

    [Header("Impact")]
    [SerializeField] private bool shakeCameraOnImpact = true;
    [SerializeField] private float impactShakeDuration = 0.08f;
    [SerializeField] private float impactShakeStrength = 0.08f;
    [SerializeField] private float randomZRotation = 4f;

    private readonly List<ParticleSystem> extraParticles = new List<ParticleSystem>();
    private Coroutine playCoroutine;

    private void Awake()
    {
        CacheExtraParticles();
        StopAndClearAll();
    }

    private void OnEnable()
    {
        if (playCoroutine != null)
            StopCoroutine(playCoroutine);

        CacheExtraParticles();
        StopAndClearAll();

        if (randomZRotation > 0f)
        {
            Vector3 euler = transform.localEulerAngles;
            euler.z += Random.Range(-randomZRotation, randomZRotation);
            transform.localEulerAngles = euler;
        }

        playCoroutine = StartCoroutine(PlayTimeline());
    }

    private void OnDisable()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
    }

    private IEnumerator PlayTimeline()
    {
        yield return WaitSeconds(impactDelay);

        PlayNow(branchGlow);
        PlayNow(impactBurst);

        if (playUnassignedChildParticles)
        {
            for (int i = 0; i < extraParticles.Count; i++)
                PlayNow(extraParticles[i]);
        }

        if (shakeCameraOnImpact && CameraShake.Instance != null)
            CameraShake.Instance.Shake(impactShakeDuration, impactShakeStrength);

        yield return WaitSeconds(destroyDelay);
        Destroy(gameObject);
    }

    private void CacheExtraParticles()
    {
        extraParticles.Clear();

        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem ps = particles[i];
            if (ps == null || ps == branchGlow || ps == impactBurst)
                continue;

            extraParticles.Add(ps);
        }
    }

    private void StopAndClearAll()
    {
        StopAndClear(branchGlow);
        StopAndClear(impactBurst);

        for (int i = 0; i < extraParticles.Count; i++)
            StopAndClear(extraParticles[i]);
    }

    private void StopAndClear(ParticleSystem ps)
    {
        if (ps == null)
            return;

        if (clearBeforePlay)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        else
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void PlayNow(ParticleSystem ps)
    {
        if (ps == null)
            return;

        ps.gameObject.SetActive(true);
        ps.Clear(true);
        ps.Play(true);
    }

    private IEnumerator WaitSeconds(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        float timer = 0f;
        while (timer < seconds)
        {
            timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }
    }
}
