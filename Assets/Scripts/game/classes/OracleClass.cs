public class OracleClass : CharacterClassBase
{
    private readonly OracleClassConfig config;

    public OracleClass(OracleClassConfig config)
    {
        this.config = config;
    }

    public override string ClassName => config.className;
    public override int MaxHP => config.maxHP;
    public override int SlotCount => config.slotCount;
    public override string[] SkinNames => config.skinNames;

    public override AttackActionData LightAttack => config.bolt;
    public override AttackActionData HeavyAttack => config.rift;
    public override AttackActionData LowAttack => null;

    public override ActionData Parry => config.ward;
    public override ActionData Defense => config.shift;
    public override DanceActionData Dance => config.dance;
    public override UltimateActionData Ultimate => config.sight;
}
