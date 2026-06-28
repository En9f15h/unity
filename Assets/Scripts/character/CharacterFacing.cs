using Photon.Pun;
using UnityEngine;

public class CharacterFacing : MonoBehaviourPun
{
    [SerializeField] private Transform visualRoot;

    private void Start()
    {
        ApplyFacing();
    }

    public void ApplyFacing()
    {
        Transform target = visualRoot != null ? visualRoot : transform;

        if (photonView.Owner == null)
            return;

        Vector3 scale = target.localScale;

        if (photonView.Owner.IsMasterClient)
        {
            scale.x = Mathf.Abs(scale.x);
        }
        else
        {
            scale.x = -Mathf.Abs(scale.x);
        }

        target.localScale = scale;
    }
}