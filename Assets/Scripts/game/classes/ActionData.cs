using UnityEngine;

[System.Serializable]
public class ActionData
{
    public string actionName;
    public ActionType actionType;
    public GameObject sourcePrefab;

    [Header("Planning")]
    [Min(1)] public int slotCost = 1;

    [Header("Button UI")]
    public Sprite iconSprite;
    public Sprite disabledOverlaySprite;
    public Sprite cooldownOverlaySprite;
    public Sprite usedOverlaySprite;
    public Sprite lockedContinuationSprite;
    public string tooltipTitle;
    [TextArea] public string tooltipDescription;
    public string rangeText;

    public int GetSlotCost()
    {
        return Mathf.Max(1, slotCost);
    }
}
