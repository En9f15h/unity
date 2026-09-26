using UnityEngine;

public static class CombatShaderMaterials
{
    private static Material oracleParticles, oracleEnergy, ghost, knightSlash;
    public static Material OracleParticles => oracleParticles != null ? oracleParticles : oracleParticles = Resources.Load<Material>("Combat/Materials/OracleParticles");
    public static Material OracleEnergy => oracleEnergy != null ? oracleEnergy : oracleEnergy = Resources.Load<Material>("Combat/Materials/OracleEnergy");
    public static Material Ghost => ghost != null ? ghost : ghost = Resources.Load<Material>("Combat/Materials/OracleGhost");
    public static Material KnightSlash => knightSlash != null ? knightSlash : knightSlash = Resources.Load<Material>("Combat/Materials/KnightSlash");
}
