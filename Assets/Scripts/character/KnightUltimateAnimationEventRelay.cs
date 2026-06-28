using UnityEngine;

public class KnightUltimateAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private KnightUltimateVFX knightUltimateVFX;

    private void Awake()
    {
        if (knightUltimateVFX == null)
            knightUltimateVFX = GetComponentInParent<KnightUltimateVFX>(true);
    }

    // µ¹ Animation Event ©I¥s
    public void OnUltimateLightningCue()
    {
        if (knightUltimateVFX != null)
            knightUltimateVFX.PlayUltimateLightning();
    }
}