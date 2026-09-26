using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ClaimSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image lockedContinuationImage;
    [SerializeField] private Image resultImage;
    [SerializeField] private Text indexText;
    [SerializeField] private Text resultText;

    private ClaimController owner;
    private ClaimSlot nextSlot;
    private ActionData currentActionData;
    private ActionType currentActionType = ActionType.None;
    private GameObject currentPlacedObject;
    private ActionType lockedActionType = ActionType.None;
    private int sourceSlotIndex = -1;
    private bool isLocalSlot;
    private bool isLockedContinuation;
    private bool hasResolvedReferences;
    private ClaimResultState resultState = ClaimResultState.Unknown;

    public int SlotIndex { get; private set; }
    public bool IsLockedContinuation => isLockedContinuation;
    public int SourceSlotIndex => sourceSlotIndex;
    public ActionType ClaimActionType => isLockedContinuation ? lockedActionType : currentActionType;
    public ClaimSlotState State
    {
        get
        {
            if (isLockedContinuation)
                return ClaimSlotState.LockedContinuation;

            return currentActionType == ActionType.None ? ClaimSlotState.Empty : ClaimSlotState.Action;
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    public void Initialize(ClaimController claimOwner, int index, bool localSlot)
    {
        owner = claimOwner;
        SlotIndex = index;
        isLocalSlot = localSlot;
        ResolveReferences();
        RefreshVisuals();
    }

    public void SetNextSlot(ClaimSlot slot)
    {
        nextSlot = slot;
    }

    public void SetVisualSize(Vector2 size)
    {
        ResolveReferences();

        if (size.x <= 0f || size.y <= 0f)
            return;

        RectTransform rect = GetComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
    }

    public void SetInteractable(bool value)
    {
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.interactable = value;
        canvasGroup.blocksRaycasts = value || !isLocalSlot;
    }

    public void Clear()
    {
        ClearPlacedItemOnly(false);
        SetLockedContinuation(false, null, ActionType.None, -1, false);
        SetResult(ClaimResultState.Unknown);
        RefreshVisuals();
    }

    public void SetRemoteState(ClaimSlotState state, ActionType actionType, int sourceIndex, Sprite sprite)
    {
        ClearPlacedItemOnly(false);
        isLockedContinuation = state == ClaimSlotState.LockedContinuation;
        sourceSlotIndex = isLockedContinuation ? sourceIndex : -1;
        lockedActionType = isLockedContinuation ? actionType : ActionType.None;
        currentActionType = state == ClaimSlotState.Action ? actionType : ActionType.None;
        currentActionData = null;

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        RefreshVisuals();
    }

    public void SetResult(ClaimResultState value)
    {
        resultState = value;
        RefreshResult();
    }

    public void DetachPlacedItemForDrag(DraggableItem item)
    {
        DetachPlacedItemForDrag(item, true);
    }

    public void DetachPlacedItemForDrag(DraggableItem item, bool notify)
    {
        if (item == null || currentPlacedObject != item.gameObject)
            return;

        if (RequiresMultipleSlots(currentActionData, currentActionType) && nextSlot != null)
            nextSlot.SetLockedContinuation(false, null, ActionType.None, -1, false);

        currentPlacedObject = null;
        currentActionData = null;
        currentActionType = ActionType.None;
        SetResult(ClaimResultState.Unknown);
        RefreshVisuals();

        if (notify)
            NotifyChanged(nextSlot);
    }

    public void RestoreDraggedItem(DraggableItem item)
    {
        if (item == null)
            return;

        ApplyPlacement(item.gameObject, item.GetActionData(), item.GetActionType(), true);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!CanEdit())
            return;

        GameObject dropped = ActionDragSource.CurrentDraggedClone;
        if (dropped == null)
            dropped = eventData.pointerDrag;

        if (dropped == null || isLockedContinuation)
            return;

        DraggableItem incomingItem = dropped.GetComponent<DraggableItem>();
        if (incomingItem == null)
            return;

        ActionData incomingData = incomingItem.GetActionData();
        ActionType incomingType = incomingItem.GetActionType();
        ActionSlot sourceActionSlot = incomingItem.GetOwnerSlot();
        ClaimSlot sourceClaimSlot = incomingItem.GetOwnerClaimSlot();

        if (incomingData == null)
            return;

        if (sourceClaimSlot == this)
        {
            RestoreDraggedItem(incomingItem);
            return;
        }

        if (!CanAcceptAction(incomingData, incomingType, sourceClaimSlot))
        {
            RestoreRejectedDrag(incomingItem, sourceActionSlot, sourceClaimSlot);
            return;
        }

        GameObject objectForClaim = dropped;
        DraggableItem itemForClaim = incomingItem;

        if (sourceActionSlot != null)
        {
            objectForClaim = Instantiate(dropped);
            itemForClaim = objectForClaim.GetComponent<DraggableItem>();
            if (itemForClaim == null)
                itemForClaim = objectForClaim.AddComponent<DraggableItem>();

            itemForClaim.Setup(incomingData, incomingType);
            DisableDragSources(objectForClaim);
            sourceActionSlot.RestoreDraggedItem(incomingItem);
        }

        ClearPlacedItemOnly(false);
        ApplyPlacement(objectForClaim, incomingData, incomingType, true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!CanEdit() || isLockedContinuation)
            return;

        if (HasPlacedItem())
            ClearPlacedItemOnly(true);
    }

    private bool CanEdit()
    {
        return isLocalSlot && owner != null && owner.CanEditLocalClaims;
    }

    private bool CanAcceptAction(ActionData actionData, ActionType actionType, ClaimSlot fromSlot)
    {
        if (!CanEdit() || actionData == null)
            return false;

        int slotCost = owner.GetSlotCostForClaim(actionData, actionType);
        if (slotCost <= 1)
            return true;

        if (nextSlot == null)
            return false;

        if (nextSlot == fromSlot)
            return true;

        if (nextSlot.isLockedContinuation || nextSlot.HasPlacedItem())
            return false;

        return true;
    }

    private void ApplyPlacement(GameObject obj, ActionData actionData, ActionType actionType, bool notify)
    {
        RectTransform targetRect = GetTargetRect();
        RectTransform droppedRect = obj != null ? obj.GetComponent<RectTransform>() : null;
        if (targetRect == null || droppedRect == null)
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
        SetResult(ClaimResultState.Unknown);

        if (iconImage != null)
            iconImage.enabled = false;

        DraggableItem item = obj.GetComponent<DraggableItem>();
        if (item != null)
            item.MarkDroppedInClaimSlot(this);

        if (RequiresMultipleSlots(actionData, actionType) && nextSlot != null)
            nextSlot.SetLockedContinuation(true, actionData, actionType, SlotIndex, false);

        RefreshVisuals();

        if (notify)
            NotifyChanged(nextSlot);
    }

    private void ClearPlacedItemOnly(bool notify)
    {
        CacheCurrentPlacedObject();

        if (currentPlacedObject != null)
            Destroy(currentPlacedObject);

        if (RequiresMultipleSlots(currentActionData, currentActionType) && nextSlot != null)
            nextSlot.SetLockedContinuation(false, null, ActionType.None, -1, false);

        currentPlacedObject = null;
        currentActionData = null;
        currentActionType = ActionType.None;
        SetResult(ClaimResultState.Unknown);
        RefreshVisuals();

        if (notify)
            NotifyChanged(nextSlot);
    }

    private void SetLockedContinuation(bool value, ActionData sourceData, ActionType actionType, int sourceIndex, bool notify)
    {
        ClearPlacedItemOnly(false);
        isLockedContinuation = value;
        sourceSlotIndex = value ? sourceIndex : -1;
        lockedActionType = value ? actionType : ActionType.None;
        currentActionData = null;
        currentActionType = ActionType.None;
        SetResult(ClaimResultState.Unknown);

        if (iconImage != null)
        {
            iconImage.sprite = value && owner != null
                ? owner.GetSpriteForClaim(sourceData, actionType, false)
                : null;
            iconImage.enabled = value && iconImage.sprite != null;
        }

        RefreshVisuals();

        if (notify)
            NotifyChanged();
    }

    private void RestoreRejectedDrag(DraggableItem incomingItem, ActionSlot sourceActionSlot, ClaimSlot sourceClaimSlot)
    {
        if (sourceActionSlot != null)
        {
            sourceActionSlot.RestoreDraggedItem(incomingItem);
            return;
        }

        if (sourceClaimSlot != null)
            sourceClaimSlot.RestoreDraggedItem(incomingItem);
    }

    private bool RequiresMultipleSlots(ActionData actionData, ActionType actionType)
    {
        if (owner == null)
            return actionType == ActionType.HeavyAttack ||
                   actionType == ActionType.Rift ||
                   actionType == ActionType.Shift ||
                   actionType == ActionType.Fade;

        return owner.GetSlotCostForClaim(actionData, actionType) >= 2;
    }

    private bool HasPlacedItem()
    {
        CacheCurrentPlacedObject();
        return currentPlacedObject != null;
    }

    private void CacheCurrentPlacedObject()
    {
        if (currentPlacedObject != null)
            return;

        RectTransform root = GetTargetRect();
        DraggableItem item = root != null ? root.GetComponentInChildren<DraggableItem>(true) : null;
        if (item == null)
            return;

        currentPlacedObject = item.gameObject;
        currentActionData = item.GetActionData();
        currentActionType = item.GetActionType();
    }

    private RectTransform GetTargetRect()
    {
        ResolveReferences();
        return contentRoot != null ? contentRoot : GetComponent<RectTransform>();
    }

    private void NotifyChanged(params ClaimSlot[] relatedSlots)
    {
        if (owner != null)
            owner.NotifyLocalClaimChanged(this, relatedSlots);
    }

    private void DisableDragSources(GameObject target)
    {
        if (target == null)
            return;

        ActionDragSource[] dragSources = target.GetComponentsInChildren<ActionDragSource>(true);
        for (int i = 0; i < dragSources.Length; i++)
        {
            if (dragSources[i] != null)
                dragSources[i].enabled = false;
        }
    }

    private void ResolveReferences()
    {
        if (hasResolvedReferences)
            return;

        RectTransform rect = GetComponent<RectTransform>();
        if (rect.rect.width <= 0f || rect.rect.height <= 0f)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100f);
        }

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (backgroundImage == null)
            backgroundImage = gameObject.AddComponent<Image>();

        backgroundImage.color = new Color(0.05f, 0.13f, 0.17f, 0.95f);
        backgroundImage.raycastTarget = true;

        if (contentRoot == null)
        {
            GameObject content = new GameObject("Content", typeof(RectTransform));
            contentRoot = content.GetComponent<RectTransform>();
            contentRoot.SetParent(transform, false);
            contentRoot.anchorMin = new Vector2(0.14f, 0.14f);
            contentRoot.anchorMax = new Vector2(0.86f, 0.86f);
            contentRoot.offsetMin = Vector2.zero;
            contentRoot.offsetMax = Vector2.zero;
        }

        if (iconImage == null)
            iconImage = CreateImage("Icon", contentRoot, false);

        if (lockedContinuationImage == null)
            lockedContinuationImage = CreateImage("LockedContinuation", transform as RectTransform, false);

        if (resultImage == null)
            resultImage = CreateImage("ClaimResult", transform as RectTransform, false);
        if (resultImage != null)
        {
            RectTransform resultRect = resultImage.rectTransform;
            resultRect.anchorMin = new Vector2(0.08f, 0f);
            resultRect.anchorMax = new Vector2(0.92f, 0.14f);
            resultRect.offsetMin = Vector2.zero;
            resultRect.offsetMax = Vector2.zero;
        }

        if (indexText == null)
            indexText = CreateText("Index", transform as RectTransform, new Vector2(0f, 0.68f), new Vector2(0.34f, 1f), 10);

        if (resultText == null)
            resultText = CreateText("ResultText", resultImage.rectTransform, Vector2.zero, Vector2.one, 10);

        hasResolvedReferences = true;
    }

    private Image CreateImage(string name, RectTransform parent, bool raycastTarget)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = obj.GetComponent<Image>();
        image.raycastTarget = raycastTarget;
        image.color = Color.white;
        return image;
    }

    private Text CreateText(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, int fontSize)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = obj.GetComponent<Text>();
        text.font = GetDefaultFont();
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        return font;
    }

    private void RefreshVisuals()
    {
        ResolveReferences();

        if (indexText != null)
            indexText.text = (SlotIndex + 1).ToString();

        if (lockedContinuationImage != null)
        {
            lockedContinuationImage.enabled = isLockedContinuation;
            lockedContinuationImage.color = new Color(0f, 0f, 0f, 0.45f);
        }

        if (iconImage != null)
        {
            if (isLockedContinuation)
            {
                iconImage.enabled = iconImage.sprite != null;
                iconImage.color = new Color(1f, 1f, 1f, 0.45f);
            }
            else if (currentActionType == ActionType.None && currentPlacedObject == null)
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.color = Color.white;
            }
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = isLockedContinuation
                ? new Color(0.05f, 0.08f, 0.1f, 0.72f)
                : new Color(0.05f, 0.13f, 0.17f, 0.95f);
        }

        RefreshResult();
    }

    private void RefreshResult()
    {
        if (resultImage == null)
            return;

        bool hasResult = resultState != ClaimResultState.Unknown;
        resultImage.enabled = hasResult;

        if (!hasResult)
        {
            if (resultText != null)
                resultText.text = string.Empty;
            return;
        }

        if (resultState == ClaimResultState.Truth)
        {
            resultImage.color = new Color(0.15f, 0.85f, 0.45f, 0.84f);
            if (resultText != null)
                resultText.text = string.Empty;
        }
        else
        {
            resultImage.color = new Color(0.95f, 0.25f, 0.22f, 0.84f);
            if (resultText != null)
                resultText.text = string.Empty;
        }
    }
}
