using UnityEngine;

public class KnightUltimateVFX : MonoBehaviour
{
    [Header("Lightning Strike Effect")]
    [SerializeField] private Transform lightningStrikePoint;
    [SerializeField] private GameObject lightningStrikePrefab;
    [SerializeField] private float lightningStrikeLifeTime = 1.0f;
    [SerializeField] private float lightningStrikeRandomScale = 0.08f;

    [Header("Sword Residual Lightning")]
    [SerializeField] private Transform swordLightningAttachPoint;
    [SerializeField] private GameObject swordResidualLightningTemplate;
    [SerializeField] private float swordResidualLifeTime = 0.8f;

    [Header("Sword Lightning Start / End Points")]
    [SerializeField] private Transform swordLightningStartPoint;
    [SerializeField] private Transform swordLightningEndPoint;

    [Header("Residual Lightning Adjustment")]
    [SerializeField] private Vector3 swordResidualLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 swordResidualLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 swordResidualLocalScale = Vector3.one;

    [Header("Impact Feel")]
    [SerializeField] private bool shakeCameraOnLightning = true;
    [SerializeField] private float lightningShakeDuration = 0.12f;
    [SerializeField] private float lightningShakeStrength = 0.12f;

    [Header("Trigger Guard")]
    [SerializeField] private bool preventDuplicateCue = true;
    [SerializeField] private float duplicateCueWindow = 0.05f;
    [SerializeField] private bool debugLogs = false;

    private GameObject currentSwordResidualInstance;
    private float lastCueTime = -999f;

    // AnimationEvent is sent to the Animator GameObject.
    // Keeping KnightUltimateVFX on the same object avoids missing relay receiver issues in prefabs.
    public void OnUltimateLightningCue()
    {
        PlayUltimateLightning();
    }

    public void PlayUltimateLightning()
    {
        if (preventDuplicateCue && Time.unscaledTime - lastCueTime < duplicateCueWindow)
            return;

        lastCueTime = Time.unscaledTime;

        if (debugLogs)
            Debug.Log("[KnightUltimateVFX] PlayUltimateLightning() called.", this);

        PlayLightningStrike();
        PlaySwordResidualLightning();

        if (shakeCameraOnLightning && CameraShake.Instance != null)
            CameraShake.Instance.Shake(lightningShakeDuration, lightningShakeStrength);
    }

    private void PlayLightningStrike()
    {
        if (lightningStrikePoint == null)
        {
            Debug.LogWarning("[KnightUltimateVFX] lightningStrikePoint is not assigned.", this);
            return;
        }

        if (lightningStrikePrefab == null)
        {
            Debug.LogWarning("[KnightUltimateVFX] lightningStrikePrefab is not assigned.", this);
            return;
        }

        GameObject fx = Instantiate(
            lightningStrikePrefab,
            lightningStrikePoint.position,
            lightningStrikePoint.rotation
        );

        float randomScale = 1f + Random.Range(-lightningStrikeRandomScale, lightningStrikeRandomScale);
        fx.transform.localScale *= Mathf.Max(0.1f, randomScale);
        fx.SetActive(true);

        PlayParticleSystems(fx);

        if (debugLogs)
            Debug.Log("[KnightUltimateVFX] Spawned lightning strike effect: " + fx.name, this);

        if (lightningStrikeLifeTime > 0f)
            Destroy(fx, lightningStrikeLifeTime);
    }

    private void PlaySwordResidualLightning()
    {
        if (swordLightningAttachPoint == null)
        {
            Debug.LogWarning("[KnightUltimateVFX] swordLightningAttachPoint is not assigned.", this);
            return;
        }

        if (swordResidualLightningTemplate == null)
        {
            Debug.LogWarning("[KnightUltimateVFX] swordResidualLightningTemplate is not assigned.", this);
            return;
        }

        if (swordLightningStartPoint == null || swordLightningEndPoint == null)
        {
            Debug.LogWarning("[KnightUltimateVFX] swordLightningStartPoint or swordLightningEndPoint is not assigned.", this);
            return;
        }

        if (currentSwordResidualInstance != null)
            Destroy(currentSwordResidualInstance);

        currentSwordResidualInstance = Instantiate(
            swordResidualLightningTemplate,
            swordLightningAttachPoint
        );

        currentSwordResidualInstance.name = "SwordResidualLightningRuntime";
        currentSwordResidualInstance.SetActive(true);
        currentSwordResidualInstance.transform.localPosition = swordResidualLocalPosition;
        currentSwordResidualInstance.transform.localRotation = Quaternion.Euler(swordResidualLocalEuler);
        currentSwordResidualInstance.transform.localScale = swordResidualLocalScale;

        SwordBoneChainLightningFX[] chainFXs =
            currentSwordResidualInstance.GetComponentsInChildren<SwordBoneChainLightningFX>(true);

        for (int i = 0; i < chainFXs.Length; i++)
        {
            if (chainFXs[i] != null)
                chainFXs[i].SetSwordPoints(swordLightningStartPoint, swordLightningEndPoint);
        }

        PlayParticleSystems(currentSwordResidualInstance);

        if (debugLogs)
            Debug.Log("[KnightUltimateVFX] Spawned sword residual lightning: " + currentSwordResidualInstance.name, this);

        Destroy(currentSwordResidualInstance, swordResidualLifeTime);
    }

    private void PlayParticleSystems(GameObject root)
    {
        if (root == null)
            return;

        ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null)
                continue;

            particleSystems[i].gameObject.SetActive(true);
            particleSystems[i].Clear(true);
            particleSystems[i].Play(true);
        }
    }
}
