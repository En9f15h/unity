using UnityEngine;

public sealed class CombatShaderClock : MonoBehaviour
{
    private static readonly int TimeId = Shader.PropertyToID("_CombatVisualTime");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        GameObject clock = new GameObject("Combat Shader Clock");
        DontDestroyOnLoad(clock);
        clock.AddComponent<CombatShaderClock>();
        Shader.SetGlobalFloat(TimeId, Time.unscaledTime);
    }
    private void Update() => Shader.SetGlobalFloat(TimeId, Time.unscaledTime);
}
