using UnityEngine;

public class BloodHitVFXManager : MonoBehaviour
{
    [Header("Blood Spray Prefab")]
    [SerializeField] private GameObject bloodSprayPrefab;
    [SerializeField] private float bloodSprayLifeTime = 1.0f;
    [SerializeField] private Vector3 bloodSprayOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField] private int bloodSprayBurstCount = 2;
    [SerializeField] private float sprayFanAngle = 18f;
    [SerializeField] private float sprayRandomOffsetRadius = 0.12f;
    [SerializeField] private float sprayRandomScale = 0.18f;

    [Header("Blood Decal Prefab")]
    [SerializeField] private GameObject bloodDecalPrefab;
    [SerializeField] private Vector3 bloodDecalOffset = new Vector3(0f, 0.02f, 0f);
    [SerializeField] private int bloodDecalCount = 2;
    [SerializeField] private float decalRandomX = 0.12f;
    [SerializeField] private float decalRandomY = 0.04f;
    [SerializeField] private float decalRandomScale = 0.15f;
    [SerializeField] private float decalRandomRotation = 18f;
    [SerializeField] private bool addFadeIfMissing = true;

    [Header("Damage Effect")]
    [SerializeField] private int brustPerDamage=3;
    [SerializeField] private float scalePerDamage=0.02f;

    public enum FacingAxis
    {
        Right,
        Up,
        Forward
    }

    [Header("Blood Spray Facing Axis")]
    [SerializeField] private FacingAxis sprayFacingAxis = FacingAxis.Right;

    public void PlayBloodHit(Transform attacker, Transform victim, int damage)
    {
        if (attacker == null || victim == null)
            return;

        Vector3 dir = victim.position - attacker.position;
        dir.z = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.right;

        dir.Normalize();

        SpawnBloodSpray(victim, dir,damage);
        SpawnBloodDecal(victim, dir);
    }

    private void SpawnBloodSpray(Transform victim, Vector3 dir,int damage)
    {
        if (bloodSprayPrefab == null)
            return;

        int count = Mathf.Max(1, bloodSprayBurstCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 variedDir = RotateDirection2D(dir, Random.Range(-sprayFanAngle, sprayFanAngle));
            Vector3 randomOffset = new Vector3(
                Random.Range(-sprayRandomOffsetRadius, sprayRandomOffsetRadius),
                Random.Range(-sprayRandomOffsetRadius, sprayRandomOffsetRadius),
                0f
            );

            Vector3 spawnPos = GetHitPoint(victim) + bloodSprayOffset + randomOffset;
            GameObject fx = Instantiate(bloodSprayPrefab, spawnPos, Quaternion.identity);

            
            float randomScale = 0.5f + Random.Range(-sprayRandomScale, sprayRandomScale);
            fx.transform.localScale *= Mathf.Max(0.1f, randomScale)+ DamageImpact(damage,fx.GetComponent<ParticleSystem>());

            ApplyDirection(fx.transform, variedDir);
            PlayParticleSystems(fx);

            Destroy(fx, bloodSprayLifeTime);
        }
    }
    private float DamageImpact(int damage,ParticleSystem fx)
    {
        float increaseScale=damage* scalePerDamage;
        fx.emission.SetBurst(0, new ParticleSystem.Burst(fx.emission.GetBurst(0).time, fx.emission.burstCount + damage * brustPerDamage));     
        return increaseScale;
    }
    private void SpawnBloodDecal(Transform victim, Vector3 dir)
    {
        if (bloodDecalPrefab == null)
            return;

        int count = Mathf.Max(1, bloodDecalCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 footPoint = GetFootPoint(victim) + bloodDecalOffset;
            footPoint.x += Random.Range(-decalRandomX, decalRandomX);
            footPoint.y += Random.Range(-decalRandomY, decalRandomY);

            Quaternion rotation = Quaternion.Euler(0f, 0f, Random.Range(-decalRandomRotation, decalRandomRotation));
            GameObject decal = Instantiate(bloodDecalPrefab, footPoint, rotation);

            float randomScale = 1f + Random.Range(-decalRandomScale, decalRandomScale);
            Vector3 s = decal.transform.localScale * Mathf.Max(0.1f, randomScale);

            if (dir.x < 0f)
                s.x = -Mathf.Abs(s.x);
            else
                s.x = Mathf.Abs(s.x);

            decal.transform.localScale = s;

            // Add fade behavior when the prefab does not manage its own lifetime.
            BloodDecalFade fade = decal.GetComponent<BloodDecalFade>();
            if (addFadeIfMissing && fade == null)
                fade = decal.AddComponent<BloodDecalFade>();

            if (fade != null)
                fade.RestartFadeFromCurrentTransform();
        }
    }

    private void ApplyDirection(Transform t, Vector3 dir)
    {
        switch (sprayFacingAxis)
        {
            case FacingAxis.Right:
                t.right = dir;
                break;

            case FacingAxis.Up:
                t.up = dir;
                break;

            case FacingAxis.Forward:
                t.forward = dir;
                break;
        }
    }

    private Vector3 RotateDirection2D(Vector3 dir, float degrees)
    {
        Quaternion rotation = Quaternion.Euler(0f, 0f, degrees);
        Vector3 rotated = rotation * dir;
        rotated.z = 0f;
        return rotated.sqrMagnitude > 0.0001f ? rotated.normalized : dir;
    }

    private void PlayParticleSystems(GameObject root)
    {
        if (root == null)
            return;

        ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null)
                continue;

            particles[i].gameObject.SetActive(true);
            particles[i].Clear(true);
            particles[i].Play(true);
        }
    }

    private Vector3 GetHitPoint(Transform target)
    {
        Collider2D col2D = target.GetComponentInChildren<Collider2D>();
        if (col2D != null)
            return col2D.bounds.center;

        Collider col3D = target.GetComponentInChildren<Collider>();
        if (col3D != null)
            return col3D.bounds.center;

        Renderer rd = target.GetComponentInChildren<Renderer>();
        if (rd != null)
            return rd.bounds.center;

        return target.position;
    }

    private Vector3 GetFootPoint(Transform target)
    {
        Collider2D col2D = target.GetComponentInChildren<Collider2D>();
        if (col2D != null)
            return new Vector3(col2D.bounds.center.x, col2D.bounds.min.y, target.position.z);

        Collider col3D = target.GetComponentInChildren<Collider>();
        if (col3D != null)
            return new Vector3(col3D.bounds.center.x, col3D.bounds.min.y, target.position.z);

        Renderer rd = target.GetComponentInChildren<Renderer>();
        if (rd != null)
            return new Vector3(rd.bounds.center.x, rd.bounds.min.y, target.position.z);

        return target.position;
    }
}
