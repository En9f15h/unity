using UnityEngine;

public class DirectionalHealthBarUI : MonoBehaviour
{
    [SerializeField] private Transform fill;
    [SerializeField] private bool shrinkToLeft = false; // true=往左縮, false=往右縮

    private float fullScaleX;
    private Vector3 fillStartLocalPos;

    private void Awake()
    {
        if (fill != null)
        {
            fullScaleX = fill.localScale.x;
            fillStartLocalPos = fill.localPosition;
        }
    }

    public void Init(int currentHP, int maxHP)
    {
        SetHP(currentHP, maxHP);
    }

    public void SetHP(int currentHP, int maxHP)
    {
        if (fill == null || maxHP <= 0)
            return;

        float ratio = Mathf.Clamp01((float)currentHP / maxHP);

        Vector3 scale = fill.localScale;
        scale.x = fullScaleX * ratio;
        fill.localScale = scale;

        // 讓縮放方向固定
        float offset = (fullScaleX - scale.x) * 0.5f;

        Vector3 pos = fillStartLocalPos;
        if (shrinkToLeft)
            pos.x = fillStartLocalPos.x + offset;
        else
            pos.x = fillStartLocalPos.x - offset;

        fill.localPosition = pos;
    }
}