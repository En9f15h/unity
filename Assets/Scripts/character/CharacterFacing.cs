using Photon.Pun;
using UnityEngine;

public class CharacterFacing : MonoBehaviourPun
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool applyAfterAnimator = true;

    private float currentDirection = 1f;
    private bool hasDirection;

    private void Start()
    {
        ApplyFacing();
    }

    private void LateUpdate()
    {
        if (!applyAfterAnimator || !hasDirection)
            return;

        ApplyDirectionToVisual();
    }

    public void ApplyFacing()
    {
        if (photonView.Owner == null)
            return;

        FaceDirection(photonView.Owner.IsMasterClient ? 1f : -1f);
    }

    public void FaceTarget(Transform target)
    {
        if (target == null)
            return;

        float delta = target.position.x - transform.position.x;
        if (Mathf.Abs(delta) < 0.001f)
            return;

        FaceDirection(Mathf.Sign(delta));
    }

    public void FaceDirection(float direction)
    {
        if (Mathf.Abs(direction) < 0.001f)
            return;

        currentDirection = Mathf.Sign(direction);
        hasDirection = true;
        ApplyDirectionToVisual();
    }

    private void ApplyDirectionToVisual()
    {
        Transform target = visualRoot != null ? visualRoot : transform;
        Vector3 scale = target.localScale;
        scale.x = Mathf.Abs(scale.x) * currentDirection;
        target.localScale = scale;
    }
}
