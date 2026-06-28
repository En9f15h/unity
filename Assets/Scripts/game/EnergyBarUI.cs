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

    private void Awake()
    {
        if (fillRect != null)
            fullWidth = fillRect.sizeDelta.x;

        ApplyRotation();
    }

    public void Init(int maxEnergy, int currentEnergy)
    {
        this.maxEnergy = Mathf.Max(1, maxEnergy);
        this.currentEnergy = Mathf.Clamp(currentEnergy, 0, this.maxEnergy);

        if (fillRect != null)
            fullWidth = fillRect.sizeDelta.x;

        ApplyRotation();
        RefreshUI();
    }

    public void SetEnergy(int currentEnergy, int maxEnergy)
    {
        this.maxEnergy = Mathf.Max(1, maxEnergy);
        this.currentEnergy = Mathf.Clamp(currentEnergy, 0, this.maxEnergy);
        RefreshUI();
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
}