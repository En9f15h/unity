using UnityEngine;

public class AutoDestroyEffect : MonoBehaviour
{
    [SerializeField] private float lifeTime = 0.5f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool playChildParticlesOnEnable = true;

    private float timer;

    private void OnEnable()
    {
        timer = 0f;

        if (!playChildParticlesOnEnable)
            return;

        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null)
                continue;

            particles[i].gameObject.SetActive(true);
            particles[i].Clear(true);
            particles[i].Play(true);
        }
    }

    private void Update()
    {
        if (lifeTime <= 0f)
            return;

        timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (timer >= lifeTime)
            Destroy(gameObject);
    }
}
