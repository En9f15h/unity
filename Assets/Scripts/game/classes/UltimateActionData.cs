using UnityEngine;

[System.Serializable]
public class UltimateActionData : ActionData
{
    [Header("基礎數值")]
    public int damage = 10;
    public int range = 2; 
    [Header("防禦互動")]
    public bool canBeParried = false;
    public bool canBeDefended = true;

    [Header("對跳躍的反應")]
    public JumpInteractionType jumpInteraction = JumpInteractionType.None;

    [Header("蓄力設定")]
    public bool requiresCharge = false;   // 只有重攻擊會用到
    public int chargeTurns = 1;           // 第1回合蓄力，第2回合出招 => 1
}