using System.Collections;
using UnityEngine;

public class BattleAnimationController : MonoBehaviour
{

    public IEnumerator PlayStep(
        CharacterUnit myUnit,
        CharacterUnit enemyUnit,
        Animator myAnimator,
        Animator enemyAnimator,
        ActionType myAction,
        ActionType enemyAction)
    {
        // 動畫、節拍、移動都放這裡
        yield return null;
    }
}