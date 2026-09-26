using UnityEngine;

public class KnightUltimateAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private KnightUltimateVFX knightUltimateVFX;

    private void Awake()
    {
        if (knightUltimateVFX == null)
            knightUltimateVFX = GetComponentInParent<KnightUltimateVFX>(true);
    }

    // Legacy manual relay entry point; KnightUltimateVFX now receives AnimationEvents directly.
    public void RelayUltimateLightningCue()
    {
        if (knightUltimateVFX != null)
            knightUltimateVFX.PlayUltimateLightning();
    }
}
