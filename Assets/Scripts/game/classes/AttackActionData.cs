using UnityEngine;

public enum JumpInteractionType
{
    None,
    Evade,      // 被跳躍完全躲掉
    HalfDamage  // 跳起來時傷害減半
}

[System.Serializable]
public class AttackActionData : ActionData
{
    [Header("基礎數值")]
    public int damage = 1;
    public int range = 1;

    [Header("防禦互動")]
    public bool canBeParried = false;
    public bool canBeDefended = true;

    [Header("對跳躍的反應")]
    public JumpInteractionType jumpInteraction = JumpInteractionType.None;

    [Header("蓄力設定")]
    public bool requiresCharge = false;   // 只有重攻擊會用到
    public int chargeTurns = 1;           // 第1回合蓄力，第2回合出招 => 1
}