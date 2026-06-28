using UnityEngine;

public class KnightUltimateVFX : MonoBehaviour
{
    [Header("落雷特效")]
    [SerializeField] private Transform lightningStrikePoint;
    [SerializeField] private GameObject lightningStrikePrefab;
    [SerializeField] private float lightningStrikeLifeTime = 1.0f;

    [Header("劍上殘留閃電")]
    [SerializeField] private Transform swordLightningAttachPoint;
    [SerializeField] private GameObject swordResidualLightningTemplate;
    [SerializeField] private float swordResidualLifeTime = 0.8f;

    [Header("劍身閃電起點 / 終點")]
    [SerializeField] private Transform swordLightningStartPoint;
    [SerializeField] private Transform swordLightningEndPoint;

    
    [Header("殘留閃電微調")]
    [SerializeField] private Vector3 swordResidualLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 swordResidualLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 swordResidualLocalScale = Vector3.one;

    private GameObject currentSwordResidualInstance;

    private void Awake()
    {
      
    }



    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    public void PlayUltimateLightning()
    {
       

        Debug.Log("[KnightUltimateVFX] PlayUltimateLightning() 被呼叫", this);

        PlayLightningStrike();
        PlaySwordResidualLightning();
    }

    private void PlayLightningStrike()
    {
        if (lightningStrikePoint == null)
        {
            Debug.LogWarning("[KnightUltimateVFX] lightningStrikePoint 沒有指定", this);
            return;
        }

        if (lightningStrikePrefab == null)
        {
            Debug.LogWarning("[KnightUltimateVFX] lightningStrikePrefab 沒有指定", this);
            return;
        }

        GameObject fx = Instantiate(
            lightningStrikePrefab,
            lightningStrikePoint.position,
            lightningStrikePoint.rotation
        );

        fx.SetActive(true);

        Debug.Log("[KnightUltimateVFX] 已生成落雷特效: " + fx.name, this);

        if (lightningStrikeLifeTime > 0f)
            Destroy(fx, lightningStrikeLifeTime);
    }

    private void PlaySwordResidualLightning()
    {
        if (swordLightningAttachPoint == null)
        {
            Debug.LogWarning("[KnightUltimateVFX] swordLightningAttachPoint 沒有指定", this);
            return;
        }

        if (swordResidualLightningTemplate == null)
        {
            Debug.LogWarning("[KnightUltimateVFX] swordResidualLightningTemplate 沒有指定", this);
            return;
        }

        if (swordLightningStartPoint == null || swordLightningEndPoint == null)
        {
            Debug.LogWarning("[KnightUltimateVFX] swordLightningStartPoint 或 swordLightningEndPoint 沒有指定", this);
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

        ParticleSystem[] particleSystems =
            currentSwordResidualInstance.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Play(true);
        }

        Debug.Log("[KnightUltimateVFX] 已生成劍上殘留閃電: " + currentSwordResidualInstance.name, this);

        Destroy(currentSwordResidualInstance, swordResidualLifeTime);
    }
}