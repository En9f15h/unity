using UnityEngine;

public class BloodHitVFXManager : MonoBehaviour
{
    [Header("¼Q¦å prefab")]
    [SerializeField] private GameObject bloodSprayPrefab;
    [SerializeField] private float bloodSprayLifeTime = 1.0f;
    [SerializeField] private Vector3 bloodSprayOffset = new Vector3(0f, 0.8f, 0f);

    [Header("¦å¸ñ prefab")]
    [SerializeField] private GameObject bloodDecalPrefab;
    [SerializeField] private Vector3 bloodDecalOffset = new Vector3(0f, 0.02f, 0f);
    [SerializeField] private float decalRandomX = 0.12f;
    [SerializeField] private float decalRandomScale = 0.15f;

    public enum FacingAxis
    {
        Right,
        Up,
        Forward
    }

    [Header("¼Q¦å´Â¦V¶b")]
    [SerializeField] private FacingAxis sprayFacingAxis = FacingAxis.Right;

    public void PlayBloodHit(Transform attacker, Transform victim)
    {
        if (attacker == null || victim == null)
            return;

        Vector3 dir = victim.position - attacker.position;
        dir.z = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.right;

        dir.Normalize();

        SpawnBloodSpray(victim, dir);
        SpawnBloodDecal(victim, dir);
    }

    private void SpawnBloodSpray(Transform victim, Vector3 dir)
    {
        if (bloodSprayPrefab == null)
            return;

        Vector3 spawnPos = GetHitPoint(victim) + bloodSprayOffset;
        GameObject fx = Instantiate(bloodSprayPrefab, spawnPos, Quaternion.identity);

        ApplyDirection(fx.transform, dir);

        Destroy(fx, bloodSprayLifeTime);
    }

    private void SpawnBloodDecal(Transform victim, Vector3 dir)
    {
        if (bloodDecalPrefab == null)
            return;

        Vector3 footPoint = GetFootPoint(victim) + bloodDecalOffset;
        footPoint.x += Random.Range(-decalRandomX, decalRandomX);

        GameObject decal = Instantiate(bloodDecalPrefab, footPoint, Quaternion.identity);

        float randomScale = 1f + Random.Range(-decalRandomScale, decalRandomScale);
        Vector3 s = decal.transform.localScale;
        s = s * randomScale;

        if (dir.x < 0f)
            s.x = -Mathf.Abs(s.x);
        else
            s.x = Mathf.Abs(s.x);

        decal.transform.localScale = s;
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