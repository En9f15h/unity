using UnityEngine;

public abstract class ActionEffect : ScriptableObject
{
    public abstract void Apply(BattleContext context);
}