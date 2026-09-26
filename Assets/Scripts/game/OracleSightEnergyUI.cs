using UnityEngine;
using UnityEngine.UI;

public class OracleSightEnergyUI : EnergyBarUI
{
    [Header("Sight Eye Display")]
    [SerializeField] private Image eyeImage;
    [SerializeField] private Sprite[] eyeStageSprites = new Sprite[5];
    [SerializeField] private Sprite activatedEyeSprite;
    [SerializeField] private GameObject ActiveImage;
    [SerializeField] private GameObject fullEnergyEffect;
    [SerializeField] private GameObject sightActiveEffect;
    [SerializeField] private CanvasGroup optionalCanvasGroup;
    [SerializeField] private bool verboseEnergyLogs;

    [Header("Sight Active Polish")]
    [SerializeField, Range(0f, 1f)] private float sightPersistentGlowStrength = 0.22f;
    [SerializeField] private float sightPersistentPulseSpeed = 2.2f;
    [SerializeField] private float sightActiveScalePulse = 0.035f;

    private bool sightActive;
    private Color eyeBaseColor = Color.white;
    private Vector3 eyeBaseScale = Vector3.one;
    private bool eyeBaseCached;
    private TurnPlanningManager energySource;
    private bool boundToLocalSide = true;
    private int boundActorNumber = -1;

    protected override void Awake()
    {
        base.Awake();
        ResolveSightReferences();
        RefreshSightUI();
    }

    private void Update()
    {
        UpdateSightActivePulse();
    }

    private void OnDestroy()
    {
        UnbindEnergySource();
    }

    public override void Init(int maxEnergy, int currentEnergy)
    {
        base.Init(maxEnergy, currentEnergy);
        RefreshSightUI();
    }

    public override void SetEnergy(int currentEnergy, int maxEnergy)
    {
        base.SetEnergy(currentEnergy, maxEnergy);
        RefreshSightUI();
    }

    protected override void OnEnergyChanged()
    {
        RefreshSightUI();
    }

    public void SetSightActive(bool active)
    {
        if (sightActive == active)
        {
            RefreshSightUI();
            return;
        }

        sightActive = active;
        if (verboseEnergyLogs && active)
            Debug.Log("[OracleEnergyUI] Sight activated. Using activated eye sprite.");

        RefreshSightUI();
    }

    public void BindToEnergySource(TurnPlanningManager source, bool bindToLocalSide, int actorNumber)
    {
        if (energySource == source && boundToLocalSide == bindToLocalSide && boundActorNumber == actorNumber)
        {
            RefreshFromEnergySource();
            return;
        }

        UnbindEnergySource();

        energySource = source;
        boundToLocalSide = bindToLocalSide;
        boundActorNumber = actorNumber;

        if (energySource != null)
        {
            energySource.EnergyChanged += HandleEnergyChanged;
            energySource.SightStateChanged += HandleSightStateChanged;
        }

        RefreshFromEnergySource();

        if (verboseEnergyLogs && energySource != null)
        {
            Debug.Log($"[OracleEnergyUI] Bound localSide={boundToLocalSide} Actor={boundActorNumber} Current={CurrentEnergy} Max={MaxEnergy}");
        }
    }

    private void UnbindEnergySource()
    {
        if (energySource != null)
        {
            energySource.EnergyChanged -= HandleEnergyChanged;
            energySource.SightStateChanged -= HandleSightStateChanged;
        }

        energySource = null;
    }

    private void HandleEnergyChanged(bool isLocalSide, int currentEnergy, int maxEnergy)
    {
        if (isLocalSide != boundToLocalSide)
            return;

        SetEnergy(currentEnergy, maxEnergy);
        RefreshSightActiveFromSource();

        if (verboseEnergyLogs)
        {
            Debug.Log($"[OracleEnergyUI] Energy changed Current={currentEnergy} Max={maxEnergy} EyeIndex={GetEnergyStageSpriteIndex()}");
        }
    }

    private void HandleSightStateChanged(int actorNumber, bool active)
    {
        if (actorNumber != boundActorNumber)
            return;

        SetSightActive(active);

        if (verboseEnergyLogs)
            Debug.Log($"[OracleEnergyUI] Sight state changed Actor={actorNumber} Active={active}");
    }

    private void RefreshFromEnergySource()
    {
        if (energySource == null)
            return;

        SetEnergy(
            energySource.GetCurrentEnergyForUI(boundToLocalSide),
            energySource.GetMaxEnergyForUI());

        RefreshSightActiveFromSource();
    }

    private void RefreshSightActiveFromSource()
    {
        if (energySource == null)
            return;

        SetSightActive(energySource.IsSightActiveOrUsedForUI(boundActorNumber));
    }

    private void ResolveSightReferences()
    {
        if (eyeImage == null)
            eyeImage = FindImageByName("EyeImage", "Eye", "SightEye");

        if (optionalCanvasGroup == null)
            optionalCanvasGroup = GetComponent<CanvasGroup>();

        CacheEyeBaseState();
    }

    private void RefreshSightUI()
    {
        ResolveSightReferences();

        if (eyeImage != null)
        {
            Sprite sprite = sightActive ? activatedEyeSprite : GetEnergyStageSprite();
            
            if (sprite != null) 
                eyeImage.sprite = sprite;
  
                    
            
            eyeImage.enabled = eyeImage.sprite != null;
            UIShaderFeedback.Ensure(eyeImage, "UI_Rune").SetState(
                (float)CurrentEnergy / Mathf.Max(1, MaxEnergy), sightActive || CurrentEnergy >= MaxEnergy);

            if (!sightActive)
                RestoreEyeBaseState();
        }

        bool fullEnergy = CurrentEnergy >= MaxEnergy;
        if (ActiveImage != null)
            ActiveImage.SetActive(fullEnergy);

        if (fullEnergyEffect != null)
            fullEnergyEffect.SetActive(!sightActive && fullEnergy);

        if (sightActiveEffect != null)
            sightActiveEffect.SetActive(sightActive);

        if (optionalCanvasGroup != null)
            optionalCanvasGroup.alpha = 1f;
    }

    private void CacheEyeBaseState()
    {
        if (eyeBaseCached || eyeImage == null)
            return;

        eyeBaseColor = eyeImage.color;
        eyeBaseScale = eyeImage.rectTransform.localScale;
        eyeBaseCached = true;
    }

    private void RestoreEyeBaseState()
    {
        if (!eyeBaseCached || eyeImage == null)
            return;

        eyeImage.color = eyeBaseColor;
        eyeImage.rectTransform.localScale = eyeBaseScale;
    }

    private void UpdateSightActivePulse()
    {
        if (!sightActive || eyeImage == null)
            return;

        CacheEyeBaseState();

        float pulse = (Mathf.Sin(Time.unscaledTime * sightPersistentPulseSpeed) + 1f) * 0.5f;
        float glow = pulse * sightPersistentGlowStrength;
        Color targetColor = Color.Lerp(eyeBaseColor, new Color(0.62f, 0.92f, 1f, eyeBaseColor.a), glow);
        eyeImage.color = targetColor;

        float scale = 1f + sightActiveScalePulse * pulse;
        eyeImage.rectTransform.localScale = eyeBaseScale * scale;
    }

    private Sprite GetEnergyStageSprite()
    {
        if (eyeStageSprites == null || eyeStageSprites.Length == 0)
            return null;

        return eyeStageSprites[GetEnergyStageSpriteIndex()];
    }

    private int GetEnergyStageSpriteIndex()
    {
        if (eyeStageSprites == null || eyeStageSprites.Length == 0)
            return 0;

        float normalizedEnergy = MaxEnergy > 0 ? (float)CurrentEnergy / MaxEnergy : 0f;
        int spriteIndex = Mathf.RoundToInt(normalizedEnergy * (eyeStageSprites.Length - 1));
        return Mathf.Clamp(spriteIndex, 0, eyeStageSprites.Length - 1);
    }

    private Image FindImageByName(params string[] names)
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null)
                continue;

            for (int n = 0; n < names.Length; n++)
            {
                if (images[i].name == names[n])
                    return images[i];
            }
        }

        return null;
    }
}
