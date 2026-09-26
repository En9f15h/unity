using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ActionSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("Slot Index")]
    public int slotIndex;

    [Header("Index Label")]
    [SerializeField] private Text indexText;

    [Header("Skill Content Container")]
    [SerializeField] private RectTransform contentRoot;

    [Header("Multi-Slot Extension")]
    [SerializeField] private ActionSlot nextSlot;

    [Header("Lock Overlay")]
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private Image lockedContinuationImage;

    [Header("Current Action Data")]
    [SerializeField] private ActionData currentActionData;

    private ActionType currentActionType = ActionType.None;
    private GameObject currentPlacedObject;
    private ActionData extensionActionData;
    private bool hasResolvedReferences;

    public bool IsOccupiedByHeavyExtension { get; private set; } = false;

    public ActionData GetExtensionActionData()
    {
        return extensionActionData;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        RefreshIndexText();
        RefreshLockOverlay();
        CacheCurrentPlacedObject();
    }

    public void ResolveReferences()
    {
        if (hasResolvedReferences)
            return;

        if (indexText == null)
            indexText = FindComponentByName<Text>("index", "number", "slottext", "text");

        if (contentRoot == null)
            contentRoot = FindComponentByName<RectTransform>("content", "itemroot", "droproot", "slotroot", "iconroot");

        if (lockOverlay == null)
        {
            Transform overlay = FindTransformByName("lock", "overlay", "disabled", "block");
            if (overlay != null)
                lockOverlay = overlay.gameObject;
        }

        if (lockedContinuationImage == null)
            lockedContinuationImage = FindComponentByName<Image>("continuation", "extension", "lockedicon", "locked");

        hasResolvedReferences = true;
    }

    public ActionData GetCurrentActionData()
    {
        return currentActionData;
    }

    public ActionType GetCurrentActionType()
    {
        return currentActionType;
    }

    public void ClearCurrentActionData()
    {
        currentActionData = null;
        currentActionType = ActionType.None;
    }

    public void SetIndex(int index)
    {
        slotIndex = index;
        ResolveReferences();
        RefreshIndexText();
    }

    public void SetNextSlot(ActionSlot slot)
    {
        nextSlot = slot;
    }

    private RectTransform GetTargetRect()
    {
        ResolveReferences();
        return contentRoot != null ? contentRoot : GetComponent<RectTransform>();
    }

    private void RefreshIndexText()
    {
        if (indexText != null)
            indexText.text = (slotIndex + 1).ToString();
    }

    private void RefreshLockOverlay()
    {
        // Keep legacy prefab references, replacing only their visual presentation.
        if (lockOverlay != null)
            lockOverlay.SetActive(false);
        if (lockedContinuationImage != null)
        {
            lockedContinuationImage.gameObject.SetActive(false);
            lockedContinuationImage.sprite = null;
        }
        ActionContinuationPresentation.Refresh(this, IsOccupiedByHeavyExtension,
            IsOccupiedByHeavyExtension ? GetLockedContinuationSprite(extensionActionData) : null);
    }

    private Sprite GetLockedContinuationSprite(ActionData actionData)
    {
        if (actionData == null)
            return null;

        if (actionData.lockedContinuationSprite != null)
            return actionData.lockedContinuationSprite;

        if (actionData.iconSprite != null)
            return actionData.iconSprite;

        if (actionData.sourcePrefab == null)
            return null;

        Image image = actionData.sourcePrefab.GetComponent<Image>();
        if (image == null || image.sprite == null)
            image = actionData.sourcePrefab.GetComponentInChildren<Image>(true);

        return image != null ? image.sprite : null;
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

    private Transform FindTransformByName(params string[] tokens)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] == null || transforms[i] == transform)
                continue;

            string normalized = Normalize(transforms[i].name);
            for (int j = 0; j < tokens.Length; j++)
            {
                if (normalized.Contains(tokens[j]))
                    return transforms[i];
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

    public void SetHeavyExtensionOccupied(bool value)
    {
        SetHeavyExtensionOccupied(value, null);
    }

    public void SetHeavyExtensionOccupied(bool value, ActionData sourceAction)
    {
        SetHeavyExtensionOccupied(value, sourceAction, true);
    }

    public void SetHeavyExtensionOccupied(bool value, ActionData sourceAction, bool notify)
    {
        IsOccupiedByHeavyExtension = value;
        extensionActionData = value ? sourceAction : null;
        RefreshLockOverlay();
        if (value) PlanningSlotFeedback.Confirm(this, true);
        else GetComponent<PlanningSlotFeedback>()?.Clear();

        if (notify)
            NotifySlotChanged();
    }

    public bool HasPlacedItem()
    {
        CacheCurrentPlacedObject();
        return currentPlacedObject != null;
    }

    private void CacheCurrentPlacedObject()
    {
        if (currentPlacedObject != null)
            return;

        RectTransform root = GetTargetRect();
        DraggableItem placed = root.GetComponentInChildren<DraggableItem>(true);
        if (placed != null)
        {
            currentPlacedObject = placed.gameObject;

            if (currentActionData == null)
                currentActionData = placed.GetActionData();

            if (currentActionType == ActionType.None)
                currentActionType = placed.GetActionType();
        }
    }

    private int GetActionSlotCost(ActionData actionData, ActionType actionType)
    {
        if (TurnPlanningManager.Instance != null)
            return TurnPlanningManager.Instance.GetSlotCostForAction(actionData, actionType);

        if (actionData != null)
            return actionData.GetSlotCost();

        if (actionType == ActionType.HeavyAttack ||
            actionType == ActionType.Rift ||
            actionType == ActionType.Shift)
        {
            return 2;
        }

        return 1;
    }

    private bool RequiresTwoSlots(ActionData actionData, ActionType actionType)
    {
        return GetActionSlotCost(actionData, actionType) >= 2;
    }

    private bool CanPlaceHeavyHere(ActionSlot ignoreSlot = null)
    {
        if (nextSlot == null)
            return false;

        if (nextSlot == ignoreSlot)
            return true;

        if (nextSlot.IsOccupiedByHeavyExtension)
            return false;

        if (nextSlot.HasPlacedItem())
            return false;

        return true;
    }

    public bool CanAcceptAction(ActionData actionData, ActionType actionType, ActionSlot fromSlot = null, bool forAutoFill = false)
    {
        if (actionData == null)
            return false;

        if (IsOccupiedByHeavyExtension && fromSlot != this)
            return false;

        if (TurnPlanningManager.Instance != null &&
            !TurnPlanningManager.Instance.CanDragOrPlaceAction(actionData, this))
            return false;

        if (forAutoFill && HasPlacedItem())
            return false;

        if (TurnPlanningManager.Instance != null &&
            TurnPlanningManager.Instance.WouldCreateConsecutiveJump(actionType, this, fromSlot))
        {
            ShowPlacementWarning("[ActionSlot] Jump cannot be used in consecutive action slots.");
            return false;
        }

        if (RequiresTwoSlots(actionData, actionType))
        {
            ActionSlot ignore = null;
            if (fromSlot != null && fromSlot.nextSlot == nextSlot)
                ignore = fromSlot;

            if (!CanPlaceHeavyHere(ignore))
                return false;
        }

        return true;
    }

    private void ShowPlacementWarning(string message)
    {
        Debug.LogWarning(message);

        // Route future placement warning UI from here to avoid scattering object names through drag/drop flow.
    }

    private void ApplyPlacement(GameObject obj, ActionData actionData, ActionType actionType)
    {
        ApplyPlacement(obj, actionData, actionType, true);
    }

    private void ApplyPlacement(GameObject obj, ActionData actionData, ActionType actionType, bool notify)
    {
        RectTransform targetRect = GetTargetRect();
        RectTransform droppedRect = obj.GetComponent<RectTransform>();

        if (droppedRect == null || targetRect == null)
            return;

        droppedRect.SetParent(targetRect, false);
        droppedRect.anchoredPosition = Vector2.zero;
        droppedRect.localScale = Vector3.one;
        droppedRect.localRotation = Quaternion.identity;

        droppedRect.anchorMin = new Vector2(0.5f, 0.5f);
        droppedRect.anchorMax = new Vector2(0.5f, 0.5f);
        droppedRect.pivot = new Vector2(0.5f, 0.5f);

        droppedRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetRect.rect.width);
        droppedRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetRect.rect.height);

        currentPlacedObject = obj;
        currentActionData = actionData;
        currentActionType = actionType;

        DraggableItem item = obj.GetComponent<DraggableItem>();
        if (item != null)
            item.MarkDroppedInSlot(this);

        if (RequiresTwoSlots(actionData, actionType) && nextSlot != null)
            nextSlot.SetHeavyExtensionOccupied(true, actionData, false);

        PlanningSlotFeedback.Confirm(this);
        if (notify)
            NotifySlotChanged(nextSlot);
    }

    public void DetachPlacedItemForDrag(DraggableItem item)
    {
        DetachPlacedItemForDrag(item, true);
    }

    public void DetachPlacedItemForDrag(DraggableItem item, bool notify)
    {
        if (item == null)
            return;

        if (currentPlacedObject == item.gameObject)
        {
            GetComponent<PlanningSlotFeedback>()?.Clear();
            if (RequiresTwoSlots(currentActionData, currentActionType) && nextSlot != null)
                nextSlot.SetHeavyExtensionOccupied(false, null, false);

            currentPlacedObject = null;
            currentActionData = null;
            currentActionType = ActionType.None;

            if (notify)
                NotifySlotChanged(nextSlot);
        }
    }

    public void RestoreDraggedItem(DraggableItem item)
    {
        if (item == null)
            return;

        ApplyPlacement(item.gameObject, item.GetActionData(), item.GetActionType());
    }

    public bool TryAutoPlaceNewObject(GameObject obj, ActionData actionData, ActionType actionType)
    {
        if (!CanAcceptAction(actionData, actionType, null, true))
            return false;

        ApplyPlacement(obj, actionData, actionType);
        return true;
    }

    public void OnDrop(PointerEventData eventData)
    {
        // Prefer the drag clone when dragging from a source palette.
        GameObject dropped = ActionDragSource.CurrentDraggedClone;

        // Without a clone, this may be an item dragged between slots.
        if (dropped == null)
            dropped = eventData.pointerDrag;

        if (dropped == null)
        {
            Debug.Log("OnDrop failed: dragged object not found.");
            return;
        }

        if (IsOccupiedByHeavyExtension)
        {
            Debug.Log("This slot is occupied by a multi-slot action and cannot accept another action.");
            return;
        }

        DraggableItem incomingItem = dropped.GetComponent<DraggableItem>();
        if (incomingItem == null)
        {
            Debug.Log("OnDrop failed: dragged object has no DraggableItem.");
            return;
        }

        ActionData incomingData = incomingItem.GetActionData();
        ActionType incomingType = incomingItem.GetActionType();
        ActionSlot fromSlot = incomingItem.GetOwnerSlot();
        ClaimSlot fromClaimSlot = incomingItem.GetOwnerClaimSlot();

        if (fromClaimSlot != null)
        {
            fromClaimSlot.RestoreDraggedItem(incomingItem);
            Debug.Log("Actual action slots do not accept Claim-only dragged items.");
            return;
        }

        if (incomingData == null)
        {
            Debug.Log("OnDrop failed: incomingData is null.");
            return;
        }

        if (!CanAcceptAction(incomingData, incomingType, fromSlot, false))
        {
            Debug.Log("This action cannot be placed now.");
            return;
        }

        // Dragged back to the original slot.
        if (fromSlot == this)
        {
            RestoreDraggedItem(incomingItem);
            return;
        }

        // Target slot is empty: place directly.
        if (!HasPlacedItem())
        {
            if (fromSlot != null)
                fromSlot.DetachPlacedItemForDrag(incomingItem, false);

            ApplyPlacement(dropped, incomingData, incomingType, false);
            NotifySlotChanged(nextSlot, fromSlot, fromSlot != null ? fromSlot.nextSlot : null);
            return;
        }

        // Target slot is occupied: swap items.
        DraggableItem targetItem = GetTargetRect().GetComponentInChildren<DraggableItem>(true);
        if (targetItem == null)
        {
            if (fromSlot != null)
                fromSlot.DetachPlacedItemForDrag(incomingItem, false);

            ApplyPlacement(dropped, incomingData, incomingType, false);
            NotifySlotChanged(nextSlot, fromSlot, fromSlot != null ? fromSlot.nextSlot : null);
            return;
        }

        ActionData targetData = targetItem.GetActionData();
        ActionType targetType = targetItem.GetActionType();

        // Palette source: overwrite the target slot.
        if (fromSlot == null)
        {
            ClearPlacedItemOnly(false);
            ApplyPlacement(dropped, incomingData, incomingType, false);
            NotifySlotChanged(nextSlot);
            return;
        }

        // Check whether the source slot can accept the target action before swapping.
        if (!fromSlot.CanAcceptAction(targetData, targetType, this, false))
        {
            Debug.Log("Swap failed: source slot cannot accept the target action.");
            fromSlot.RestoreDraggedItem(incomingItem);
            return;
        }

        this.DetachPlacedItemForDrag(targetItem, false);
        fromSlot.DetachPlacedItemForDrag(incomingItem, false);

        ApplyPlacement(dropped, incomingData, incomingType, false);
        fromSlot.ApplyPlacement(targetItem.gameObject, targetData, targetType, false);
        NotifySlotChanged(nextSlot, fromSlot, fromSlot.nextSlot);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (HasPlacedItem())
            ClearPlacedItemOnly();
    }

    public void ClearPlacedItemOnly()
    {
        ClearPlacedItemOnly(true);
    }

    public void ClearPlacedItemOnly(bool notify)
    {
        GetComponent<PlanningSlotFeedback>()?.Clear();
        CacheCurrentPlacedObject();

            if (currentPlacedObject != null)
        {
            if (RequiresTwoSlots(currentActionData, currentActionType) && nextSlot != null)
                nextSlot.SetHeavyExtensionOccupied(false, null, false);

            Destroy(currentPlacedObject);
        }

        currentPlacedObject = null;
        currentActionData = null;
        currentActionType = ActionType.None;

        if (notify)
            NotifySlotChanged(nextSlot);
    }

    private void NotifySlotChanged(params ActionSlot[] relatedSlots)
    {
        if (TurnPlanningManager.Instance != null)
            TurnPlanningManager.Instance.NotifyLocalPlanningSlotsChanged(this, relatedSlots);
    }
}
