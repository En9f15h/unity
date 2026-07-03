using UnityEngine;

public class DirectionalHealthBarUI : MonoBehaviour
{
    [SerializeField] private Transform fill;
    [SerializeField] private bool shrinkToLeft = false; // true=往左縮, false=往右縮

    private float fullScaleX;
    private Vector3 fillStartLocalPos;
    private bool hasCachedFillStartState;
    private bool hasResolvedReferences;

    private void Awake()
    {
        ResolveReferences();
        CacheFillStartState();
    }

    public void ResolveReferences()
    {
        if (hasResolvedReferences)
            return;

        if (fill == null)
            fill = FindTransformByName("fill", "bar", "gauge");

        hasResolvedReferences = true;
    }

    private void CacheFillStartState()
    {
        if (fill != null)
        {
            fullScaleX = fill.localScale.x;
            fillStartLocalPos = fill.localPosition;
            hasCachedFillStartState = true;
        }
    }

    public void Init(int currentHP, int maxHP)
    {
        SetHP(currentHP, maxHP);
    }

    public void SetHP(int currentHP, int maxHP)
    {
        ResolveReferences();

        if (fill == null || maxHP <= 0)
            return;

        if (!hasCachedFillStartState)
            CacheFillStartState();

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
}
