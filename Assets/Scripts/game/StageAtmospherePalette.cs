using UnityEngine;

public sealed class StageAtmospherePalette : ScriptableObject
{
    [System.Serializable] public struct Entry { public Sprite background; public Color color; [Range(0,0.3f)] public float density; }
    public Entry[] entries;
    public bool TryGet(Sprite sprite,out Entry entry)
    {
        if(entries!=null) foreach(var item in entries) if(item.background==sprite && sprite!=null) { entry=item; return true; }
        entry=default; return false;
    }
}
