public class BattleContext
{
    public TurnPlanningManager manager;
    public CharacterUnit attacker;
    public CharacterUnit defender;
    public CharacterClassConfig attackerConfig;
    public CharacterClassConfig defenderConfig;
    public ActionType attackerActionType;
    public ActionType defenderActionType;
    public bool attackerIsMine;
}