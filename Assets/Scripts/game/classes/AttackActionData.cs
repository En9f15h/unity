using UnityEngine;

public enum JumpInteractionType
{
    None,
    Evade,      // Fully evaded while jumping
    HalfDamage  // Damage is halved while jumping
}

[System.Serializable]
public class AttackActionData : ActionData
{
    [Header("Base Values")]
    public int damage = 1;
    public int minRange = 0;
    public int range = 1;

    [Header("Defense Interaction")]
    public bool canBeParried = false;
    public bool canBeDefended = true;

    [Header("Jump Interaction")]
    public JumpInteractionType jumpInteraction = JumpInteractionType.None;

    [Header("Charge Settings")]
    public bool requiresCharge = false;   // Used by heavy attacks.
    public int chargeTurns = 1;           // Turn 1 charges, turn 2 releases.
}
