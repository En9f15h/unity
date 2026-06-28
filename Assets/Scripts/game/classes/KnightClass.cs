public class KnightClass : CharacterClassBase
{
    private CharacterClassConfig config;

    public KnightClass(CharacterClassConfig config)
    {
        this.config = config;
    }

    public override string ClassName => config.className;
    public override int MaxHP => config.maxHP;
    public override int SlotCount => config.slotCount;
    public override string[] SkinNames => config.skinNames;

    public override AttackActionData LightAttack => config.lightAttack;
    public override AttackActionData HeavyAttack => config.heavyAttack;
    public override AttackActionData LowAttack => config.lowAttack;

    public override ActionData Parry => config.parry;
    public override ActionData Defense => config.defense; 
    public override DanceActionData Dance => config.dance;
    public override UltimateActionData Ultimate => config.ultimate;
}