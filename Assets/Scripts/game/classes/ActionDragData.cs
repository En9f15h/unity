using UnityEngine;

public class ActionDragData : MonoBehaviour
{
    public const float DefaultDragWidth = 150f;
    public const float DefaultDragHeight = 150f;

    public ActionType actionType = ActionType.None;
    [Min(1f)] public float dragWidth = DefaultDragWidth;
    [Min(1f)] public float dragHeight = DefaultDragHeight;

    public Vector2 GetDragSize()
    {
        return new Vector2(
            dragWidth > 0f ? dragWidth : DefaultDragWidth,
            dragHeight > 0f ? dragHeight : DefaultDragHeight);
    }

    public static Vector2 ResolveDragSize(GameObject target)
    {
        ActionDragData dragData = target != null ? target.GetComponent<ActionDragData>() : null;
        if (dragData == null && target != null)
            dragData = target.GetComponentInChildren<ActionDragData>(true);

        return dragData != null
            ? dragData.GetDragSize()
            : new Vector2(DefaultDragWidth, DefaultDragHeight);
    }
}
