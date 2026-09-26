using UnityEngine;

public sealed class StageReadabilityPalette : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public Sprite background;
        [Range(.5f, 1.2f)] public float brightness;
        [Range(.5f, 1.2f)] public float contrast;
        [Range(0, 1.2f)] public float saturation;
        public Color edgeColor;
        [Range(0, 1)] public float edgeColorMix;
        [Range(1, 2)] public float edgeMultiplier;
        public Vector4 Grade => new Vector4(brightness, contrast, saturation, 0);
    }

    public Entry[] entries;
    public bool TryGet(Sprite sprite, out Entry entry)
    {
        if (sprite != null && entries != null)
            foreach (var item in entries)
                if (item.background == sprite) { entry = item; return true; }
        entry = default;
        return false;
    }
}
