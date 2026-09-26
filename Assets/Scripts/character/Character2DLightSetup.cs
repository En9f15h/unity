using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Character2DLightSetup : MonoBehaviour
{
    [Header("Optional Light Anchor")]
    [SerializeField] private Transform lightAnchor;

    [Header("Light Object Name")]
    [SerializeField] private string lightObjectName = "CharacterLight2D";

    [Header("Ally / Enemy Colors")]
    [SerializeField] private Color myLightColor = new Color(0.35f, 0.65f, 1f, 0.9f);     // Blue light
    [SerializeField] private Color enemyLightColor = new Color(1f, 0.35f, 0.35f, 0.9f);  // Red light

    [Header("Light Intensity")]
    [SerializeField] private float myIntensity = 0.8f;
    [SerializeField] private float enemyIntensity = 0.8f;

    [Header("Light Radius")]
    [SerializeField] private float pointLightOuterRadius = 2.2f;
    [SerializeField] private float pointLightInnerRadius = 0.8f;
    [SerializeField] private float falloffIntensity = 0.5f;

    [Header("Position Offset")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.8f, 0f);

    [Header("Optional Sorting Layer")]
    [SerializeField] private bool useTargetSortingLayers = false;
    [SerializeField] private int[] targetSortingLayerIDs;

    [Header("Apply On Awake")]
    [SerializeField] private bool applyOnStart = true;

    private Light2D runtimeLight;
    private PhotonView cachedPhotonView;

    private void Awake()
    {
        cachedPhotonView = GetComponentInParent<PhotonView>();
        if (cachedPhotonView == null)
            cachedPhotonView = GetComponentInChildren<PhotonView>(true);
    }

    private void Start()
    {
        if (applyOnStart)
            CreateOrUpdateLight();
    }

    [ContextMenu("Create Or Update Light")]
    public void CreateOrUpdateLight()
    {
        Transform anchor = lightAnchor != null ? lightAnchor : transform;

        Transform child = anchor.Find(lightObjectName);
        if (child == null)
        {
            GameObject go = new GameObject(lightObjectName);
            child = go.transform;
            child.SetParent(anchor, false);
        }

        child.localPosition = localOffset;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;

        runtimeLight = child.GetComponent<Light2D>();
        if (runtimeLight == null)
            runtimeLight = child.gameObject.AddComponent<Light2D>();

        runtimeLight.lightType = Light2D.LightType.Point;
        runtimeLight.pointLightOuterRadius = pointLightOuterRadius;
        runtimeLight.pointLightInnerRadius = pointLightInnerRadius;
        runtimeLight.falloffIntensity = falloffIntensity;

        ApplyTargetSortingLayers();

        ApplyOwnerColor();
    }

    private void ApplyTargetSortingLayers()
    {
        if (!useTargetSortingLayers ||
            targetSortingLayerIDs == null ||
            targetSortingLayerIDs.Length == 0 ||
            runtimeLight == null)
        {
            return;
        }

        System.Reflection.MethodInfo method = typeof(Light2D).GetMethod(
            "SetTargetSortingLayers",
            new System.Type[] { typeof(int[]) }
        );

        if (method != null)
            method.Invoke(runtimeLight, new object[] { targetSortingLayerIDs });
    }

    [ContextMenu("Apply Owner Color")]
    public void ApplyOwnerColor()
    {
        if (runtimeLight == null)
            CreateOrUpdateLight();

        if (runtimeLight == null)
            return;

        bool isMine = IsMineCharacter();

        runtimeLight.color = isMine ? myLightColor : enemyLightColor;
        runtimeLight.intensity = isMine ? myIntensity : enemyIntensity;
    }

    private bool IsMineCharacter()
    {
        if (cachedPhotonView == null)
        {
            cachedPhotonView = GetComponentInParent<PhotonView>();
            if (cachedPhotonView == null)
                cachedPhotonView = GetComponentInChildren<PhotonView>(true);
        }

        if (cachedPhotonView == null)
        {
            Debug.LogWarning($"{name}: PhotonView not found; defaulting to enemy red light.");
            return false;
        }

        return cachedPhotonView.IsMine;
    }

    public Light2D GetRuntimeLight()
    {
        return runtimeLight;
    }

    public void SetLightEnabled(bool value)
    {
        if (runtimeLight == null)
            CreateOrUpdateLight();

        if (runtimeLight != null)
            runtimeLight.enabled = value;
    }

    public void SetIntensity(float value)
    {
        if (runtimeLight == null)
            CreateOrUpdateLight();

        if (runtimeLight != null)
            runtimeLight.intensity = value;
    }

    public void SetColor(Color color)
    {
        if (runtimeLight == null)
            CreateOrUpdateLight();

        if (runtimeLight != null)
            runtimeLight.color = color;
    }

    [ContextMenu("Remove Light")]
    public void RemoveLight()
    {
        Transform anchor = lightAnchor != null ? lightAnchor : transform;
        Transform child = anchor.Find(lightObjectName);

        if (child != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
                Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }

        runtimeLight = null;
    }
}
