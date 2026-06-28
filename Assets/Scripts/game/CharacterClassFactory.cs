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
                return new FortuneTellerClass(configs[classIndex]);

            case 2:
                return new WarriorClass(configs[classIndex]);

            default:
                return new KnightClass(configs[0]);
        }
    }
}