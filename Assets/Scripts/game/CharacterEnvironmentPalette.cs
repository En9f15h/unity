using UnityEngine;

[CreateAssetMenu(menuName="Presentation/Character Environment Palette")]
public sealed class CharacterEnvironmentPalette : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public string label;
        public Sprite background;
        [ColorUsage(false,true)] public Color bodyLight, edgeLight;
        [Range(-1,1)] public float shadowDirection;
        [Range(.05f,.4f)] public float shadowOpacity;
        [Range(0,.8f)] public float shadowLength;
        [Range(.05f,.6f)] public float shadowDepth;
        public bool arenaBraziers;
        [Range(0,2)] public float sunlightMultiplier;
        public Color daylightColor;
        [Tooltip("World X offset from the background center, and height above ground.")]
        public Vector2 daylightOffset;
        [Range(.1f,4)] public float daylightIntensity;
        [Range(-1,1)] public float beamSlope;
        [Range(.2f,6)] public float beamWidth;
        [Range(0,.6f)] public float beamSpread;
        [Range(.5f,5)] public float beamFocus;
        [Range(0,1)] public float secondaryBeam;
        [Range(0,1)] public float crownStrength;
        [Range(0,1)] public float daylightDrift;
    }
    public Entry[] entries;
    public bool TryGet(Sprite background,out Entry value)
    {
        if(background!=null && entries!=null)
            foreach(var entry in entries)if(entry.background==background){value=entry;return true;}
        value=default;return false;
    }
}
