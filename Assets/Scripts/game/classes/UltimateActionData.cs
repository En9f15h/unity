using UnityEngine;

[System.Serializable]
public class UltimateActionData : ActionData
{
    [Header("Base Values")]
    public int damage = 10;
    public int minRange = 0;
    public int range = 2; 
    [Header("Defense Interaction")]
    public bool canBeParried = false;
    public bool canBeDefended = true;

    [Header("Jump Interaction")]
    public JumpInteractionType jumpInteraction = JumpInteractionType.None;

    [Header("Charge Settings")]
    public bool requiresCharge = false;   // Used by charged attacks if enabled.
    public int chargeTurns = 1;           // Turn 1 charges, turn 2 releases.
}
