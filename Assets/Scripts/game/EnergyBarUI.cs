using UnityEngine;
using UnityEngine.UI;

public class EnergyBarUI : MonoBehaviour
{
    [Header("Fill 顯示")]
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private Image fillImage;
    [SerializeField] private Text energyText;

    [Header("顯示方式")]
    [SerializeField] private bool useImageFillAmount = true;
    [SerializeField] private bool rotate90Degrees = false;

    private int maxEnergy = 10;
    private int currentEnergy = 0;
    private float fullWidth;
    private bool hasResolvedReferences;

    private void Awake()
    {
        ResolveReferences();
        CacheFullWidth();
        ApplyRotation();
    }

    public void ResolveReferences()
    {
        if (hasResolvedReferences)
            return;

        if (fillImage == null)
            fillImage = FindComponentByName<Image>("fill", "bar", "gauge");

        if (fillRect == null && fillImage != null)
            fillRect = fillImage.rectTransform;

        if (fillRect == null)
            fillRect = FindComponentByName<RectTransform>("fill", "bar", "gauge");

        if (energyText == null)
            energyText = FindComponentByName<Text>("energy", "value", "amount", "text");

        hasResolvedReferences = true;
    }

    public void Init(int maxEnergy, int currentEnergy)
    {
        ResolveReferences();
        this.maxEnergy = Mathf.Max(1, maxEnergy);
        this.currentEnergy = Mathf.Clamp(currentEnergy, 0, this.maxEnergy);

        CacheFullWidth();
        ApplyRotation();
        RefreshUI();
    }

    public void SetEnergy(int currentEnergy, int maxEnergy)
    {
        ResolveReferences();
        this.maxEnergy = Mathf.Max(1, maxEnergy);
        this.currentEnergy = Mathf.Clamp(currentEnergy, 0, this.maxEnergy);
        CacheFullWidth();
        RefreshUI();
    }

    private void CacheFullWidth()
    {
        if (fillRect != null && fullWidth <= 0f)
            fullWidth = fillRect.sizeDelta.x;
    }

    private void RefreshUI()
    {
        float percent = (float)currentEnergy / maxEnergy;

        if (useImageFillAmount && fillImage != null)
        {
            fillImage.fillAmount = percent;
        }
        else if (fillRect != null)
        {
            Vector2 size = fillRect.sizeDelta;
            size.x = fullWidth * percent;
            fillRect.sizeDelta = size;
        }

        if (energyText != null)
        {
            energyText.text = currentEnergy + " / " + maxEnergy;
        }
    }

    private void ApplyRotation()
    {
        if (fillRect != null)
        {
            fillRect.localRotation = rotate90Degrees
                ? Quaternion.Euler(0f, 0f, 90f)
                : Quaternion.identity;
        }
    }

    private T FindComponentByName<T>(params string[] tokens) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null || components[i].transform == transform)
                continue;

            string normalized = Normalize(components[i].name);
            for (int j = 0; j < tokens.Length; j++)
            {
                if (normalized.Contains(tokens[j]))
                    return components[i];
            }
        }

        return null;
    }

    private string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }
}
