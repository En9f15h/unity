public static class CharacterClassFactory
{
    public static CharacterClassBase CreateClass(int classIndex, CharacterClassConfig[] configs)
    {
        if (configs == null || configs.Length == 0)
            return null;

        if (classIndex < 0 || classIndex >= configs.Length)
            classIndex = 0;

        switch (classIndex)
        {
            case 0:
                return new KnightClass(configs[classIndex]);

            case 1:
                if (configs[classIndex] is OracleClassConfig oracleConfig)
                    return new OracleClass(oracleConfig);

                return new WarriorClass(configs[classIndex]);

            case 2:
                return new WarriorClass(configs[classIndex]);

            case 3:
                return new WarriorClass(configs[classIndex]);

            default:
                if (configs[classIndex] is OracleClassConfig fallbackOracleConfig)
                    return new OracleClass(fallbackOracleConfig);

                return new KnightClass(configs[0]);
        }
    }
}
